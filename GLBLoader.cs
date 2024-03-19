
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon;

namespace VoyageVoyage
{
    public class GLBLoader : UdonSharpBehaviour
    {

        public VRCUrl glbUrl;
        public MeshFilter[] filters;

        public MeshRenderer temporaryRenderer;
        public Material baseMaterial;
        

        object[] m_bufferViews;
        object[] m_accessors;
        Material[] m_materials;

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
            ParseGLB(result.ResultBytes);
        }

        public override void OnStringLoadError(IVRCStringDownload result)
        {
            ReportError("StringDownloader", $"Error loading string: {result.ErrorCode} - {result.Error}");
        }

        void Start()
        {
            VRCStringDownloader.LoadUrl(glbUrl, (VRC.Udon.Common.Interfaces.IUdonEventReceiver)this);

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

                everythingIsOk |= dictionary.ContainsKey(name);
                everythingIsOk |= (dictionary[name].TokenType == type);
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
            bool check = CheckFields(accessorInfo,
                "bufferView", TokenType.Double,
                "componentType", TokenType.Double,
                "count", TokenType.Double,
                "type", TokenType.String);
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
                (int)(double)accessorInfo["componentType"],
                (int)(double)accessorInfo["count"],
                (string)accessorInfo["type"]
                };
        }
        const int accessorBufferIndex = 0;
        const int accessorComponentTypeIndex = 1;
        const int accessorCountIndex = 2;
        const int accessorTypeIndex = 3;

        bool GetBufferViewInfo(DataToken bufferViewToken, out int byteOffset, out int byteLength, out int target)
        {
            byteLength = 0;
            byteOffset = 0;
            target = 0;
            if (bufferViewToken.TokenType != TokenType.DataDictionary) return false;

            DataDictionary bufferView = (DataDictionary)bufferViewToken;
            if (!HasKeys(bufferView, "byteLength", "byteOffset", "target")) return false;

            DataToken bufferByteLength = bufferView["byteLength"];
            DataToken bufferByteOffset = bufferView["byteOffset"];
            DataToken bufferTarget = bufferView["target"];

            if ((bufferByteLength.TokenType != TokenType.Double)
                || (bufferByteOffset.TokenType != TokenType.Double)
                || (bufferTarget.TokenType != TokenType.Double))
            {
                return false;
            }

            byteLength = (int)((double)bufferByteLength);
            byteOffset = (int)((double)bufferByteOffset);
            target = (int)((double)bufferTarget);

            ReportInfo("GLB", $"Parsing buffer : offset {byteOffset}, length {byteLength}");


            return true;
        }

        bool GetSubmeshInfo(DataDictionary primitives, out int positionsView, out int normalsView, out int uvsView, out int indicesView, out int materialIndex)
        {
            positionsView = 0;
            normalsView = 0;
            uvsView = 0;
            indicesView = 0;
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
                "NORMAL", TokenType.Double,
                "TEXCOORD_0", TokenType.Double);
            if (!check)
            {
                ReportError("Getsubmeshinfo", $"Invalid attributes in {primitives}");
                return check;
            }



            positionsView = (int)((double)attributes["POSITION"]);
            normalsView = (int)((double)attributes["NORMAL"]);
            uvsView = (int)((double)attributes["TEXCOORD_0"]);
            indicesView = (int)((double)primitives["indices"]);
            if (primitives.TryGetValue("material", TokenType.Double, out DataToken materialToken))
            {
                materialIndex = (int)(double)materialToken;
            }

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
                ReportInfo("GetMeshInfo", $"{name} : Mesh {m} - {positionsView},{normalsView},{uvsView},{indicesView},{materialIndex}");
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

            ReportInfo("LoadMeshFrom", $"positionsView : {positionsView}, normalsView : {normalsView}, uvsView : {uvsView}, indicesView : {indicesView}");

            if ((positionsView >= nAccessors) | (normalsView >= nAccessors) | (uvsView >= nAccessors) | (indicesView >= nAccessors))
            {
                ReportError("LoadMesh", $"Invalid views provided ({positionsView},{normalsView},{uvsView},{indicesView}) >= {nAccessors}");
                return m;
            }
            object positionsAccessor = m_accessors[positionsView];
            object normalsAccessor = m_accessors[normalsView];
            object uvsAccessor = m_accessors[uvsView];
            object indicesAccessor = m_accessors[indicesView];

            if ((positionsAccessor == null) | (normalsAccessor == null) | (uvsAccessor == null) | (indicesAccessor == null))
            {
                ReportError("LoadMesh", "Some buffers were null...");
                return m;
            }

            object positionsBuffer = ((object[])positionsAccessor)[accessorBufferIndex];
            object normalsBuffer = ((object[])normalsAccessor)[accessorBufferIndex];
            object uvsBuffer = ((object[])uvsAccessor)[accessorBufferIndex];
            object indicesBuffer = ((object[])indicesAccessor)[accessorBufferIndex];
            
            System.Type floatArray = typeof(float[]);
            System.Type ushortArray = typeof(ushort[]);
            if ((positionsBuffer.GetType() != floatArray)
                | (normalsBuffer.GetType() != floatArray)
                | (uvsBuffer.GetType() != floatArray)
                | (indicesBuffer.GetType() != ushortArray))
            {
                ReportError("LoadMesh", $"Some buffer views types are invalid : {positionsBuffer.GetType()}, {normalsBuffer.GetType()}, {uvsBuffer.GetType()}, {indicesBuffer.GetType()}");
                return m;
            }

            m.vertices = floatsToVector3((float[])positionsBuffer);
            m.normals = floatsToVector3((float[])normalsBuffer);
            m.uv = floatsToVector2((float[])uvsBuffer);
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

            ReportInfo("LoadMesh", $"nSubmeshes : {nSubmeshes}");

            CombineInstance[] instances = new CombineInstance[nSubmeshes];
            for (int s = 0; s < nSubmeshes; s++)
            {
                CombineInstance instance = instances[s];
                instance.transform = Matrix4x4.identity;
                instance.mesh = LoadMeshFrom(submeshesInfo, s * meshInfoNIndices);
                instance.subMeshIndex = 0;
                instances[s] = instance;
            }

            mesh.CombineMeshes(instances);
            return true;
        }

        object[] ParseBufferViews(DataList bufferViews, byte[] glb, int dataOffset)
        {
            int nViews = bufferViews.Count;
            object[] views = new object[nViews];
            for (int v = 0; v < nViews; v++)
            {
                if (GetBufferViewInfo(bufferViews[v], out int localOffset, out int nBytes, out int target))
                {
                    int offset = dataOffset + localOffset;
                    switch (target)
                    {
                        case 34962:
                            views[v] = GetFloats(glb, offset, nBytes);
                            break;
                        case 34963:
                            views[v] = GetUshorts(glb, offset, nBytes);
                            break;
                        default:
                            ReportError("GLB", $"Unhandled buffer view target {target}");
                            views[v] = null;
                            break;
                    }
                }
            }
            return views;
        }

        void ParseAndShowMeshes(DataList meshesInfo, MeshFilter[] filters)
        {
            int nFilters = filters.Length;
            int nMeshes = meshesInfo.Count;

            int f = 0;

            for (int m = 0; m < nMeshes; m++)
            {
                if (f >= nFilters) break;

                DataToken meshInfoToken = meshesInfo[m];
                if (meshInfoToken.TokenType != TokenType.DataDictionary) continue;

                bool gotAMesh = LoadMesh((DataDictionary)meshInfoToken, out string name, out Mesh mesh, out int[] materialsIndices);
                if (!gotAMesh) continue;

                filters[f].sharedMesh = mesh;
                filters[f].gameObject.name = name;
                MeshRenderer renderer = filters[f].GetComponent<MeshRenderer>();

                if (renderer != null)
                {
                    int nIndices = materialsIndices.Length;
                    Material[] sharedMaterials = new Material[nIndices];
                    int nKnownMaterials = m_materials.Length;

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
                
                f++;
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
            if (list.Count < 4) return Quaternion.identity;
            if (!IsListComponentType(list, TokenType.Double)) return Quaternion.identity;
            return new Quaternion((float)(double)list[0], (float)(double)list[1], (float)(double)list[2], (float)(double)list[3]);
        }

        Vector3 DataListToVector3(DataList list)
        {
            if (list.Count < 3) return Vector3.zero;
            if (!IsListComponentType(list, TokenType.Double)) return Vector3.zero;
            return new Vector3((float)(double)list[0], (float)(double)list[1], (float)(double)list[2]);
        }

        Color DataListToColor(DataList list)
        {
            return new Color((float)(double)list[0], (float)(double)list[1], (float)(double)list[2], (float)(double)list[3]);
        }

        bool ParseNode(DataDictionary node, out int meshIndex, out string name, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            meshIndex = 0;
            name = "";
            position = Vector3.zero;
            rotation = Quaternion.identity;
            scale = Vector3.one;
            bool check = CheckFields(node,
                "mesh", TokenType.Double,
                "name", TokenType.String);
            if (!check)
            {
                ReportError("ParseNode", $"Ill formed node ? {node}");
                return false;
            }

            meshIndex = (int)(double)node["mesh"];
            name = (string)node["name"];

            if (node.TryGetValue("translation", TokenType.DataList, out DataToken translationToken))
            {
                Vector3 glToDxPosition = DataListToVector3((DataList)translationToken);
                
                //glToDxPosition.x *= -1;
                position = glToDxPosition;
            }

            if (node.TryGetValue("rotation", TokenType.DataList, out DataToken rotationToken))
            {
                Quaternion q = DataListToQuaternion((DataList)rotationToken);
                /*q.x *= -1;
                q.w *= -1;*/
                rotation = q;
            }

            if (node.TryGetValue("scale", TokenType.DataList, out DataToken scaleToken))
            {
                scale = DataListToVector3((DataList)scaleToken);
            }

            return true;
        }

        void ParseNodes(DataList nodes)
        {
            int nNodes = nodes.Count;
            int maxMesh = filters.Length - 1;

            for (int n = 0; n < nNodes; n++)
            {
                DataToken nodeToken = nodes[n];
                if (nodeToken.TokenType != TokenType.DataDictionary) continue;
                ParseNode((DataDictionary)nodeToken, out int meshIndex, out string name, out Vector3 position, out Quaternion rotation, out Vector3 scale);
                if (meshIndex > maxMesh) return;
                filters[meshIndex].name = name;
                var transform = filters[meshIndex].transform;
                transform.localPosition = position;
                transform.localRotation = rotation;
                transform.localScale = scale;
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

        Material CreateMaterialFrom(DataDictionary materialInfo)
        {
            Material mat = NewMaterial();
            
            if (materialInfo.TryGetValue("name", TokenType.String, out DataToken stringToken))
            {
                mat.name = (string)stringToken;
            }
            if (materialInfo.TryGetValue("pbrMetallicRoughness", TokenType.DataDictionary, out DataToken pbrToken))
            {
                DataDictionary pbrInfo = (DataDictionary)pbrToken;
                if (pbrInfo.TryGetValue("baseColorFactor", TokenType.DataList, out DataToken colorToken))
                {
                    DataList colorInfo = (DataList)colorToken;
                    if (colorInfo.Count == 4 && IsListComponentType(colorInfo, TokenType.Double))
                    {
                        mat.color = DataListToColor(colorInfo);
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
            m_materials = ParseMaterials((DataList)glbJsonRoot["materials"]);
            ParseAndShowMeshes((DataList)glbJsonRoot["meshes"], filters);
            ParseNodes((DataList)glbJsonRoot["nodes"]);
            

        }
    }

}
