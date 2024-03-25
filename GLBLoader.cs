
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon;

namespace VoyageVoyage
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class GLBLoader : UdonSharpBehaviour
    {
        
        public VRCUrl glbUrl;

        [UdonSynced]
        VRCUrl syncedUrl;
        public Transform mainParent;
        public GameObject nodePrefab;

        public MeshRenderer temporaryRenderer;
        public Material baseMaterial;
        
        object[] m_bufferViews;
        object[] m_accessors;
        Material[] m_materials;
        object[] m_meshesInfo;
        Texture2D[] m_images;

        string DictOptString(DataDictionary dict, string fieldName, string defaultValue)
        {
            string retValue = defaultValue;
            if (dict.TryGetValue(fieldName, TokenType.String, out DataToken stringToken))
            {
                return (string)stringToken;
            }
            return retValue;
        }

        int DictOptInt(DataDictionary dict, string fieldName, int defaultValue)
        {
            int retValue = defaultValue;
            if (dict.TryGetValue(fieldName, TokenType.Double, out DataToken doubleToken))
            {
                return (int)(double)doubleToken;
            }

            return retValue;
        }

        Vector3 DictOptVector3(DataDictionary dict, string fieldName, Vector3 defaultValue)
        {
            Vector3 retValue = defaultValue;
            if (dict.TryGetValue(fieldName, TokenType.DataList, out DataToken dataListToken))
            {
                DataList list = (DataList)dataListToken;
                if ((list.Count >= 3) && (IsListComponentType(list, TokenType.Double)))
                {
                    retValue = DataListToVector3(list);
                }
            }
            return retValue;
        }

        Quaternion DictOptQuaternion(DataDictionary dict, string fieldName, Quaternion defaultValue)
        {
            Quaternion retValue = defaultValue;
            if (dict.TryGetValue(fieldName, TokenType.DataList, out DataToken dataListToken))
            {
                DataList list = (DataList)dataListToken;
                if ((list.Count >= 4) && (IsListComponentType(list, TokenType.Double)))
                {
                    retValue = DataListToQuaternion(list);
                }
            }
            return retValue;
        }

        void Clear()
        {
            m_bufferViews = new object[0];
            m_accessors = new object[0];
            m_materials = new Material[0];
            m_images = new Texture2D[0];
            Transform[] children = mainParent.GetComponentsInChildren<Transform>();
            int nChildren = children.Length;
            for (int c = 0; c < nChildren; c++)
            {
                /* Because, in Unity, you're a child of yourself... */
                if (children[c] != mainParent)
                {
                    Destroy(children[c].gameObject);
                }
            }
        }

        void ReportError(string tag, string message)
        {
            Debug.LogError($"<color=red>[{tag}] {message}</color>");
        }

        void ReportInfo(string tag, string message)
        {
            Debug.Log($"<color=green>[{tag}] {message}</color>");
        }

        public override void OnStringLoadSuccess(IVRCStringDownload result)
        {
            Clear();
            ParseGLB(result.ResultBytes);
        }

        public override void OnStringLoadError(IVRCStringDownload result)
        {
            ReportError("StringDownloader", $"Error loading string: {result.ErrorCode} - {result.Error}");
        }

        void Start()
        {
            //SetURL(glbUrl);
        }

        public void SetURL(VRCUrl url)
        {
            glbUrl = url;
            VRCStringDownloader.LoadUrl(glbUrl, (VRC.Udon.Common.Interfaces.IUdonEventReceiver)this);
            Sync();
        }

        void Sync()
        {
            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            if (localPlayer == null)
            {
                ReportError("VRChat", "Local player is null...");
                return;
            }

            if (Networking.GetOwner(gameObject) == localPlayer)
            {
                NowImTheOwner();
            }
            else
            {
                Networking.SetOwner(localPlayer, gameObject);
            }
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if (player == Networking.LocalPlayer)
            {
                NowImTheOwner();
            }
        }

        void NowImTheOwner()
        {
            syncedUrl = glbUrl;
            RequestSerialization();
        }

        public override void OnDeserialization()
        {
            if (syncedUrl != glbUrl)
            {
                glbUrl = syncedUrl;
                VRCStringDownloader.LoadUrl(glbUrl, (VRC.Udon.Common.Interfaces.IUdonEventReceiver)this);
            }
        }

        void DumpList(string name, DataList list)
        {
            Debug.Log($"<color=blue> Dumping list {name} !</color>");
            int nElements = list.Count;
            for (int i = 0; i < nElements; i++)
            {
                if (list.TryGetValue(i, TokenType.String, out DataToken stringValue))
                {
                    ReportInfo("GLB", $"Key : {(string)stringValue}");
                }
            }
        }

        void DumpKeys(string name, DataDictionary dict)
        {
            DumpList($"{name} keys", dict.GetKeys());
        }

        void DumpValues(string name, DataDictionary dict)
        {
            Debug.Log($"<color=blue> Dumping list {name} !</color>");
            DataList values = dict.GetValues();
            int nElements = values.Count;
            for (int e = 0; e < nElements; e++)
            {
                ReportInfo("DumpValues", $"values[{e}] = {values[e].TokenType}");
            }
        }

        bool HasKeys(DataDictionary dict, params string[] keys)
        {
            int nKeys = keys.Length;
            bool hasAllKeys = true;
            for (int k = 0; k < nKeys; k++)
            {
                hasAllKeys &= dict.ContainsKey(keys[k]);
            }
            return hasAllKeys;
        }

        bool CheckFields(DataDictionary dictionary, params object[] fieldNamesAndTypes)
        {
            bool everythingIsOk = true;
            int nObjects = fieldNamesAndTypes.Length;
            for (int i = 0; i < nObjects; i += 2)
            {
                string name = (string)fieldNamesAndTypes[i + 0];
                TokenType type = (TokenType)fieldNamesAndTypes[i + 1];
                i += 2;

                everythingIsOk &= dictionary.ContainsKey(name);
                everythingIsOk &= (dictionary[name].TokenType == type);
            }
            return everythingIsOk;
        }

        object[] ParseAccessors(DataList accessorsInfo)
        {
            int nAccessors = accessorsInfo.Count;
            object[] newAccessors = new object[nAccessors];
            for (int i = 0; i < nAccessors; i++)
            {
                DataToken accessorToken = accessorsInfo[i];
                if (accessorToken.TokenType != TokenType.DataDictionary)
                {
                    continue;
                }
                newAccessors[i] = ParseAccessor((DataDictionary)accessorToken);
            }
            return newAccessors;
        }
        object[] ParseAccessor(DataDictionary accessorInfo)
        {
            bool check = CheckFields(accessorInfo, "bufferView", TokenType.Double);
            if (!check)
            {
                ReportError("ParseAccessor", $"Unexpected format for Accessor Info : {accessorInfo}");
                return null;
            }

            int bufferViewIndex = (int)(double)accessorInfo["bufferView"];
            if (bufferViewIndex >= m_bufferViews.Length)
            {
                ReportError("ParseAccessor", $"Accessor defining a buffer view {bufferViewIndex} out of bounds (max : {m_bufferViews.Length})");
                return null;
            }

            return new object[] {
                m_bufferViews[bufferViewIndex],
                DictOptInt(accessorInfo, "componentType", -1),
                DictOptInt(accessorInfo, "count", 0),
                DictOptString(accessorInfo, "type", "")
            };
        }
        const int accessorBufferIndex = 0;
        const int accessorComponentTypeIndex = 1;
        const int accessorCountIndex = 2;
        const int accessorTypeIndex = 3;

        bool GetSubmeshInfo(DataDictionary primitives, out int positionsView, out int normalsView, out int uvsView, out int indicesView, out int materialIndex)
        {
            positionsView = -1;
            normalsView = -1;
            uvsView = -1;
            indicesView = -1;
            materialIndex = -1;

            bool check = CheckFields(primitives, 
                "attributes", TokenType.DataDictionary,
                "indices", TokenType.Double);
            if (!check)
            {
                ReportError("GetSubmeshInfo", $"Invalid fields in {primitives}");
                return check;
            }

            DataDictionary attributes = (DataDictionary)primitives["attributes"];
            check = CheckFields(attributes,
                "POSITION", TokenType.Double,
                "NORMAL", TokenType.Double);
            if (!check)
            {
                ReportError("Getsubmeshinfo", $"Invalid attributes in {primitives}");
                return check;
            }



            positionsView = (int)((double)attributes["POSITION"]);
            normalsView = (int)((double)attributes["NORMAL"]);
            indicesView = (int)((double)primitives["indices"]);

            uvsView = DictOptInt(attributes, "TEXCOORD_0", -1);
            ReportInfo("GetSubmeshInfo", $"TEXCOORD_0 : {uvsView}");
            materialIndex = DictOptInt(primitives, "material", -1);
            return true;
        }

        const int meshInfoPositionViewIndex = 0;
        const int meshInfoNormalsViewIndex = 1;
        const int meshInfoUvsViewIndex = 2;
        const int meshInfoIndicesViewIndex = 3;
        const int meshInfoNIndices = 4;
        bool GetMeshInfo(DataDictionary meshInfo, out string name, out int meshes, out int[] views, out int[] materialsIndices)
        {
            name = "";
            meshes = 0;
            views = new int[0];
            materialsIndices = new int[0];

            bool check = CheckFields(
                meshInfo,
                "name", TokenType.String,
                "primitives", TokenType.DataList);
            if (!check)
            {
                ReportError("GetMeshInfo", $"MeshInfo has an unexpected format {meshInfo}");
                return check;
            }

            name = (string)meshInfo["name"];
            DataList primitives = (DataList)meshInfo["primitives"];
            int nMeshes = primitives.Count;
            int actualNumberOfMeshes = 0;
            
            // 4 type of views info : position, normals, uv, indices
            views = new int[nMeshes * meshInfoNIndices];
            materialsIndices = new int[nMeshes];
            int v = 0;
            for (int m = 0; m < nMeshes; m++)
            {
                bool gotMeshInfoToken = primitives.TryGetValue(m, TokenType.DataDictionary, out DataToken meshInfoToken);
                if (!gotMeshInfoToken)
                {
                    continue;
                }
                bool parsedMeshInfo = GetSubmeshInfo(
                    (DataDictionary)meshInfoToken,
                    out int positionsView,
                    out int normalsView,
                    out int uvsView,
                    out int indicesView,
                    out int materialIndex);
                if (!parsedMeshInfo)
                {
                    continue;
                }

                views[v + meshInfoPositionViewIndex] = positionsView;
                views[v + meshInfoNormalsViewIndex] = normalsView;
                views[v + meshInfoUvsViewIndex] = uvsView;
                views[v + meshInfoIndicesViewIndex] = indicesView;
                materialsIndices[m] = materialIndex;
                //ReportInfo("GetMeshInfo", $"{name} : Mesh {m} - {positionsView},{normalsView},{uvsView},{indicesView},{materialIndex}");
                v += meshInfoNIndices;
                actualNumberOfMeshes += 1;
            }
            meshes = actualNumberOfMeshes;

            return true;

        }

        ushort[] GetUshorts(byte[] glbData, int offset, int nBytes)
        {
            int nUshorts = nBytes / 2;
            ushort[] ret = new ushort[nUshorts];
            System.Buffer.BlockCopy(glbData, offset, ret, 0, nBytes);
            return ret;
        }

        float[] GetFloats(byte[] glbData, int offset, int nBytes)
        {
            int nFloats = nBytes / 4;
            float[] ret = new float[nFloats];
            System.Buffer.BlockCopy(glbData, offset, ret, 0, nBytes);
            return ret;
        }

        Vector3[] floatsToVector3(float[] floats)
        {
            int nFloats = floats.Length;
            int nVectors = nFloats / 3;
            Vector3[] ret = new Vector3[nVectors];
            for (int v = 0, f = 0; v < nVectors; v++, f += 3)
            {
                ret[v].x = floats[f + 0];
                ret[v].y = floats[f + 1];
                ret[v].z = floats[f + 2];
            }
            return ret;
        }

        Vector2[] floatsToVector2(float[] floats)
        {
            int nFloats = floats.Length;
            int nVectors = nFloats / 2;
            Vector2[] ret = new Vector2[nVectors];
            for (int v = 0, f = 0; v < nVectors; v++, f += 2)
            {
                ret[v].x = floats[f + 0];
                ret[v].y = floats[f + 1];
            }
            return ret;
        }

        string Vector3ToString(Vector3 v)
        {
            return $"[{v.x},{v.y},{v.z}]";
        }

        Mesh LoadMeshFrom(int[] meshInfo, int startOffset)
        {
            Mesh m = new Mesh();
            int nAccessors = m_accessors.Length;
            int positionsView = meshInfo[startOffset + meshInfoPositionViewIndex];
            int normalsView = meshInfo[startOffset + meshInfoNormalsViewIndex];
            int uvsView = meshInfo[startOffset + meshInfoUvsViewIndex];
            int indicesView = meshInfo[startOffset + meshInfoIndicesViewIndex];

            //ReportInfo("LoadMeshFrom", $"positionsView : {positionsView}, normalsView : {normalsView}, uvsView : {uvsView}, indicesView : {indicesView}");

            if ((positionsView >= nAccessors) | (normalsView >= nAccessors) | (uvsView >= nAccessors) | (indicesView >= nAccessors))
            {
                ReportError("LoadMesh", $"Invalid views provided ({positionsView},{normalsView},{uvsView},{indicesView}) >= {nAccessors}");
                return m;
            }
            object positionsAccessor = m_accessors[positionsView];
            object normalsAccessor = m_accessors[normalsView];
            object indicesAccessor = m_accessors[indicesView];

            if ((positionsAccessor == null) | (normalsAccessor == null) | (indicesAccessor == null))
            {
                ReportError("LoadMesh", "Some buffers were null...");
                return m;
            }

            object positionsBuffer = ((object[])positionsAccessor)[accessorBufferIndex];
            object normalsBuffer = ((object[])normalsAccessor)[accessorBufferIndex];
            object indicesBuffer = ((object[])indicesAccessor)[accessorBufferIndex];

            
            System.Type floatArray = typeof(float[]);
            System.Type ushortArray = typeof(ushort[]);
            if ((positionsBuffer.GetType() != floatArray)
                | (normalsBuffer.GetType() != floatArray)
                | (indicesBuffer.GetType() != ushortArray))
            {
                ReportError("LoadMesh", $"Some buffer views types are invalid : {positionsBuffer.GetType()}, {normalsBuffer.GetType()}, {indicesBuffer.GetType()}");
                return m;
            }

            m.vertices = floatsToVector3((float[])positionsBuffer);
            m.normals = floatsToVector3((float[])normalsBuffer);

            if (uvsView > 0)
            {
                ReportInfo("LoadMeshFrom", "Got a UV view !");
                object uvsAccessor = m_accessors[uvsView];
                if (uvsAccessor != null)
                {
                    ReportInfo("LoadMeshFrom", "Got an accessor !");
                    object uvsBuffer = ((object[])uvsAccessor)[accessorBufferIndex];
                    if (uvsBuffer.GetType() == floatArray)
                    {
                        ReportInfo("LoadMeshFrom", "Setting up the UVS !");
                        m.uv = floatsToVector2((float[])uvsBuffer);
                    }
                } 
            }
            
            m.SetIndices((ushort[])indicesBuffer, MeshTopology.Triangles, 0);

            return m;

        }

        bool LoadMesh(DataDictionary meshInfo, out string name, out Mesh mesh, out int[] matIndices)
        {
            mesh = new Mesh();
            bool gotMeshInfo = GetMeshInfo(meshInfo, out name, out int nSubmeshes, out int[] submeshesInfo, out int[] materialsIndices);
            matIndices = materialsIndices;
            if (!gotMeshInfo)
            {
                ReportError("LoadMesh", "Could not get mesh informations");
                return false;
            }

            ReportInfo("LoadMesh", $"$Mesh : {name} nSubmeshes : {nSubmeshes}");

            CombineInstance[] instances = new CombineInstance[nSubmeshes];
            for (int s = 0; s < nSubmeshes; s++)
            {
                CombineInstance instance = instances[s];
                instance.transform = Matrix4x4.identity;
                instance.mesh = LoadMeshFrom(submeshesInfo, s * meshInfoNIndices);
                instance.subMeshIndex = 0;
                instances[s] = instance;
            }

            mesh.CombineMeshes(instances, false);
            return true;
        }

        void GetBufferViewInfo(DataDictionary bufferView, out int byteOffset, out int byteLength, out int target)
        {
            byteLength = DictOptInt(bufferView, "byteLength", 0);
            byteOffset = DictOptInt(bufferView, "byteOffset", 0);
            target = DictOptInt(bufferView, "target", 0);
        }

        object[] ParseBufferViews(DataList bufferViews, byte[] glb, int dataOffset)
        {
            int nViews = bufferViews.Count;
            object[] views = new object[nViews];
            for (int v = 0; v < nViews; v++)
            {
                DataToken bufferViewToken = bufferViews[v];
                if (bufferViewToken.TokenType != TokenType.DataDictionary)
                {
                    views[v] = null;
                }
                DataDictionary bufferView = (DataDictionary)bufferViewToken;
                GetBufferViewInfo(bufferView, out int localOffset, out int nBytes, out int target);

                int offset = dataOffset + localOffset;
                switch (target)
                {
                    case 0:
                        views[v] = new int[] { offset, nBytes };
                        break;
                    case 34962:
                        views[v] = GetFloats(glb, offset, nBytes);
                        break;
                    case 34963:
                        views[v] = GetUshorts(glb, offset, nBytes);
                        break;
                    default:
                        ReportInfo("GLB", $"Unhandled buffer view target {target}");
                        views[v] = null;
                        break;
                }

            }
            return views;
        }

        void ParseAndShowMeshes(DataList meshesInfo)
        {
            int nMeshes = meshesInfo.Count;

            m_meshesInfo = new object[nMeshes];

            for (int m = 0; m < nMeshes; m++)
            {
                DataToken meshInfoToken = meshesInfo[m];
                if (meshInfoToken.TokenType != TokenType.DataDictionary) continue;

                bool gotAMesh = LoadMesh((DataDictionary)meshInfoToken, out string name, out Mesh mesh, out int[] materialsIndices);
                if (!gotAMesh) continue;

                m_meshesInfo[m] = new object[] { mesh, materialsIndices };
            }
        }

        bool IsListComponentType(DataList list, TokenType type)
        {
            bool allOk = true;
            int nElements = list.Count; ;
            for (int e = 0; e < nElements; e++)
            {
                allOk |= (list[e].TokenType == type);
            }
            return allOk;
        }

        Quaternion DataListToQuaternion(DataList list)
        {
            return new Quaternion((float)(double)list[0], (float)(double)list[1], (float)(double)list[2], (float)(double)list[3]);
        }

        Vector3 DataListToVector3(DataList list)
        {
            return new Vector3((float)(double)list[0], (float)(double)list[1], (float)(double)list[2]);
        }

        Color DataListToColor(DataList list)
        {
            return new Color((float)(double)list[0], (float)(double)list[1], (float)(double)list[2], (float)(double)list[3]);
        }



        bool ParseNode(DataDictionary node, out int meshIndex, out string name, out Vector3 position, out Quaternion rotation, out Vector3 scale, out int[] children)
        {
            meshIndex = DictOptInt(node, "mesh", -1);
            name = DictOptString(node, "name", "");
            position = DictOptVector3(node, "translation", Vector3.zero);
            rotation = DictOptQuaternion(node, "rotation", Quaternion.identity);
            scale = DictOptVector3(node, "scale", Vector3.one);
            children = new int[0];

            if (node.TryGetValue("children", TokenType.DataList, out DataToken childrenToken))
            {

                DataList childrenList = (DataList)childrenToken;

                int nChildren = childrenList.Count;
                children = new int[nChildren];

                for (int c = 0; c < nChildren; c++)
                {
                    if (childrenList.TryGetValue(c, TokenType.Double, out DataToken nodeIndexToken))
                    {
                        children[c] = (int)(double)nodeIndexToken;
                    }
                    else
                    {
                        children[c] = -1;
                    }
                }
            }

            return true;
        }

        void SetupMesh(GameObject node, int meshIndex, int maxMeshIndex)
        {
            if ((meshIndex < 0) | (meshIndex > maxMeshIndex)) return;
            object meshInfoArray = m_meshesInfo[meshIndex];
            if (meshInfoArray == null) return;

            object[] meshInfo = (object[])m_meshesInfo[meshIndex];
            if (meshInfo == null) return;

            // FIXME Magic values
            Mesh mesh = (Mesh)meshInfo[0];
            int[] materialsIndices = (int[])meshInfo[1];

            if (mesh == null) return;

 
            MeshFilter filter = node.GetComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            if (materialsIndices == null) return;
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();

            int nIndices = materialsIndices.Length;
            int nKnownMaterials = m_materials.Length;

            Material[] sharedMaterials = new Material[nIndices];

            for (int sharedMatIndex = 0; sharedMatIndex < nIndices; sharedMatIndex++)
            {
                int materialIndex = materialsIndices[sharedMatIndex];
                if ((materialIndex >= 0) & (materialIndex < nKnownMaterials))
                {
                    sharedMaterials[sharedMatIndex] = m_materials[materialIndex];
                }
                else
                {
                    sharedMaterials[sharedMatIndex] = NewMaterial();
                }
            }
            renderer.sharedMaterials = sharedMaterials;

        }

        void ParseNodes(DataList nodes)
        {
            int nNodes = nodes.Count;
            int maxMeshIndex = m_meshesInfo.Length  ;

            /* Make sure we know all the nodes in advance...
             * Just in case we have forward references...
             */
            GameObject[] nodesObjects = new GameObject[nNodes];
            for (int n = 0; n < nNodes; n++)
            {
                nodesObjects[n] = Instantiate(nodePrefab, mainParent);
            }

            for (int n = 0; n < nNodes; n++)
            {
                DataToken nodeToken = nodes[n];
                if (nodeToken.TokenType != TokenType.DataDictionary) continue;
                ParseNode((DataDictionary)nodeToken, out int meshIndex, out string name, out Vector3 position, out Quaternion rotation, out Vector3 scale, out int[] children);

                GameObject node = nodesObjects[n];

                SetupMesh(node, meshIndex, maxMeshIndex);

                node.name = name;

                /* Setup the transform */
                Transform transform = node.transform;
                transform.localPosition = position;
                transform.localRotation = rotation;
                transform.localScale = scale;

                /* Parent the children */
                int nChildren = children.Length;
                for (int c = 0; c < nChildren; c++)
                {
                    int childIndex = children[c];
                    if ((childIndex < 0) | (childIndex >= nNodes))
                    {
                        continue;
                    }
                    nodesObjects[childIndex].transform.SetParent(transform, false);
                }
            }
        }

        Material NewMaterial()
        {
            /* This actually Instantiate a new material.
             * For some reason, you can't do Instantiate(material)
             * with Udon
             */
            temporaryRenderer.material = baseMaterial;

            return temporaryRenderer.material;
        }

        // Shamelessly stolen from https://forum.unity.com/threads/standard-material-shader-ignoring-setfloat-property-_mode.344557/
        public static void SetAsFade(Material material)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = 3000;
        }

        Material CreateMaterialFrom(DataDictionary materialInfo)
        {
            Material mat = NewMaterial();

            mat.name = DictOptString(materialInfo, "name", mat.name);

            if (materialInfo.TryGetValue("pbrMetallicRoughness", TokenType.DataDictionary, out DataToken pbrToken))
            {
                DataDictionary pbrInfo = (DataDictionary)pbrToken;
                if (pbrInfo.TryGetValue("baseColorTexture", TokenType.DataDictionary, out DataToken textureInfoToken))
                {
                    ReportInfo("CreateMaterialFrom", "BaseColorTexture");
                    DataDictionary textureInfo = (DataDictionary)textureInfoToken;
                    if (textureInfo.TryGetValue("index", TokenType.Double, out DataToken indexToken))
                    {
                        int textureIndex = (int)(double)indexToken;
                        if ((textureIndex >= 0) & (textureIndex < m_images.Length))
                        {
                            ReportInfo("CreateMaterialFrom", "Setting texture");
                            mat.SetTexture("_MainTex", m_images[textureIndex]);
                        }
                    }

                }
                if (pbrInfo.TryGetValue("baseColorFactor", TokenType.DataList, out DataToken colorToken))
                {
                    DataList colorInfo = (DataList)colorToken;
                    if (colorInfo.Count == 4 && IsListComponentType(colorInfo, TokenType.Double))
                    {
                        Color color = DataListToColor(colorInfo);
                        mat.color = color;
                        if (color.a < 1)
                        {
                            SetAsFade(mat);
                        }
                    }
                }
                if (pbrInfo.TryGetValue("metallic", TokenType.Double, out DataToken metallicToken))
                {
                    mat.SetFloat("_Metallic", (float)(double)metallicToken);
                }
                if (pbrInfo.TryGetValue("roughnessFactor", TokenType.Double,  out DataToken roughnessToken))
                {
                    mat.SetFloat("_Glossiness", 1.0f - ((float)(double)roughnessToken));
                }
            }
            // Let's forget about Double side for the moment...
            return mat;
        }

        Material[] ParseMaterials(DataList materialsInfo)
        {
            int nMaterials = materialsInfo.Count;
            Material[] materials = new Material[nMaterials];
            for (int m = 0; m < nMaterials; m++)
            {
                DataToken materialInfoToken = materialsInfo[m];
                if (materialInfoToken.TokenType != TokenType.DataDictionary)
                {
                    materials[m] = null;
                    continue;
                }
                materials[m] = CreateMaterialFrom((DataDictionary)materialInfoToken);
            }
            return materials;
        }

        const int offsetIndex = 0;
        const int sizeIndex = 1;
        string voyageExtensionName = "EXT_voyage_exporter";

        Texture2D ParseImage(DataDictionary imageInfo, byte[] glb)
        {
            bool checkFields = CheckFields(imageInfo,
                "bufferView", TokenType.Double,
                "extensions", TokenType.DataDictionary);
            if (!checkFields)
            {
                ReportError("ParseImage", "No bufferView or extensions");
                return null;
            }

            DataDictionary extension = (DataDictionary)imageInfo["extensions"];
            bool hasExtension = extension.TryGetValue(voyageExtensionName, TokenType.DataDictionary, out DataToken voyageExtensionToken);
            if (!hasExtension)
            {
                ReportError("ParseImage", "Invalid type for extensions");
                return null;
            }

            DataDictionary voyageExtension = (DataDictionary)voyageExtensionToken;

            int width = DictOptInt(voyageExtension, "width", -1);
            int height = DictOptInt(voyageExtension, "height", -1);
            string formatInfo = DictOptString(voyageExtension, "format", "");

            if ((width == -1) | (height == 1) | (formatInfo == ""))
            {
                ReportError("ParseImage", "No width, height or format informations on Voyage's extension");
                return null;
            }

            TextureFormat textureFormat;
            switch(formatInfo)
            {
                case "BGRA32":
                    textureFormat = TextureFormat.BGRA32;
                    break;
                default:
                    ReportError("ParseImage", "Unknown texture format");
                    return null;
            }

            int bufferViewIndex = (int)(double)imageInfo["bufferView"];
            if ((bufferViewIndex < 0) | (bufferViewIndex >= m_bufferViews.Length))
            {
                ReportError("ParseImage", $"Invalid buffer view {bufferViewIndex}");
                return null;
            }
            object bufferView = m_bufferViews[bufferViewIndex];
            if (bufferView == null)
            {
                ReportError("ParseImage", $"Buffer view {bufferViewIndex} is null");
                return null;
            }
            if (bufferView.GetType() != typeof(int[]))
            {
                ReportError("ParseImage", $"Buffer view {bufferViewIndex} has an invalid type");
                return null;
            }


            int[] offsetAndSize = (int[])bufferView;
            int offset = offsetAndSize[offsetIndex];
            int size = offsetAndSize[sizeIndex];

            byte[] textureData = new byte[size];
            Buffer.BlockCopy(glb, offset, textureData, 0, size);


            Texture2D tex = new Texture2D(width, height, textureFormat, false);
            tex.LoadRawTextureData(textureData);
            tex.Apply(false, false);
            return tex;
        }

        Texture2D[] ParseImages(DataList images, byte[] glbData)
        {
            int nImages = images.Count;
            Texture2D[] textures = new Texture2D[nImages];
            for (int i = 0; i < nImages; i++)
            {
                DataToken imageInfoToken = images[i];
                if (imageInfoToken.TokenType != TokenType.DataDictionary) continue;
                textures[i] = ParseImage((DataDictionary)imageInfoToken, glbData);
            }
            return textures;
        }

        void ParseGLB(byte[] glb)
        {
            if (glb == null)
            {
                ReportError("GLB", "No header !");
                return;
            }

            if (glb.Length < 32)
            {
                ReportError("GLB", "GLB size is wrong");
                return;
            }

            int cursor = 0;

            uint magic = System.BitConverter.ToUInt32(glb, cursor);
            cursor += 4;
            if (magic != 0x46546C67)
            {
                ReportError("GLB", $"Wrong magic. Expected 0x46546C67 but got 0x{magic:X8}");
                return;
            }

            uint version = System.BitConverter.ToUInt32(glb, cursor);
            cursor += 4;
            ReportInfo("GLB", $"GLB Version {version}");

            uint size = System.BitConverter.ToUInt32(glb, cursor);
            cursor += 4;
            ReportInfo("GLB", $"Size : {size}");

            ReportInfo("GLB", $"Data size : {glb.Length}");

            if (glb.Length < size)
            {
                ReportError("GLB", $"Not enough data in this GLB file ! Expected {size} but got {glb.Length}");
                return;
            }

            int chunkLength = System.BitConverter.ToInt32(glb, cursor);
            cursor += 4;
            uint chunkType = System.BitConverter.ToUInt32(glb, cursor);
            cursor += 4;

            if (chunkLength < 0)
            {
                ReportError("GLB", $"Negative chunk length {chunkLength}");
                return;
            }

            if (chunkType != 0x4E4F534A)
            {
                ReportError("GLB", $"Expected a JSON Chunk here !");
                return;
            }

            ReportInfo("GLB", $"Next chunk : Type : 0x{chunkType:X8} - Size {chunkLength}");

            string jsonData = System.Text.Encoding.UTF8.GetString(glb, cursor, chunkLength);
            ReportInfo("GLB", jsonData);

            cursor += chunkLength;

            chunkLength = System.BitConverter.ToInt32(glb, cursor);
            cursor += 4;
            chunkType = System.BitConverter.ToUInt32(glb, cursor);
            cursor += 4;

            if (chunkType != 0x004E4942)
            {
                ReportError("GLB", $"Expected a binary chunk here. Got a 0x{chunkType:X8}");
                return;
            }

            VRCJson.TryDeserializeFromJson(jsonData, out DataToken result);
            if (result.TokenType != TokenType.DataDictionary)
            {
                ReportError("GLB", "Invalid GLB Json data !");
                return;
            }

            DataDictionary glbJsonRoot = (DataDictionary)result;
            bool check = CheckFields(glbJsonRoot,
                "meshes", TokenType.DataList,
                "materials", TokenType.DataList,
                "bufferViews", TokenType.DataList,
                "accessors", TokenType.DataList,
                "nodes", TokenType.DataList);
            if (!check)
            {
                ReportError("GLB", "Unexpected GLB data");
                return;
            }


            m_bufferViews = ParseBufferViews((DataList)glbJsonRoot["bufferViews"], glb, cursor);
            m_accessors = ParseAccessors((DataList)glbJsonRoot["accessors"]);
            if (glbJsonRoot.TryGetValue("images", TokenType.DataList, out DataToken imagesToken))
            {
                m_images = ParseImages((DataList)glbJsonRoot["images"], glb);
            }
            m_materials = ParseMaterials((DataList)glbJsonRoot["materials"]);
            
            ParseAndShowMeshes((DataList)glbJsonRoot["meshes"]);
            ParseNodes((DataList)glbJsonRoot["nodes"]);
            

        }
    }

}
