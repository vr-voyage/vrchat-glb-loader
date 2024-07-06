
using System;
using System.Runtime.InteropServices.WindowsRuntime;
using UdonSharp;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon;

namespace VoyageVoyage
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class GLBLoader : UdonSharpBehaviour
    {
        
        public VRCUrl userURL;

        public Transform mainParent;
        public GameObject nodePrefab;

        public MeshRenderer temporaryRenderer;
        public Material baseMaterial;
        public Shader unlitShader;
        public Shader unlitShaderCutout;
        public Shader unlitShaderTransparent;
        public UdonSharpBehaviour[] stateReceivers;

        DataDictionary m_accessorTypesInfo;

        object[] m_bufferViews;
        object[] m_accessors;
        Material[] m_materials;
        object[] m_meshesInfo;
        public Texture2D[] m_images;
        GameObject[] m_nodes;

        const int errorValue = -2;
        const int sectionComplete = -1;

        int currentState = -1;
        int currentIndex = -1;
        byte[] glb = new byte[0];
        string glbJsonRaw = "";
        DataDictionary glbJson;
        int glbDataStart = 0;
        bool finished = false;
        float limit = 0;

        const int accessorFieldBufferParsed = 0;
        const int accessorFieldBufferReference = 1;
        const int accessorFieldBufferBufferView = 2;
        const int accessorFieldBufferComponentType = 3;
        const int accessorFieldBufferOffset = 4;
        const int accessorFieldBufferCount = 5;
        const int accessorFieldBufferType = 6;
        const int accessorFieldsCount = 7;

        const int bufferViewFieldBufferIndex = 0;
        const int bufferViewFieldOffset = 1;
        const int bufferViewFieldSize = 2;
        const int bufferViewFieldStride = 3;
        const int bufferViewFieldTarget = 4;
        const int bufferViewFieldsCount = 5;

        void ResetState()
        {
            currentState = -1;
            currentIndex = -1;
            glb = new byte[0];
            glbJsonRaw = "";
            glbJson = new DataDictionary();
            glbDataStart = 0;

            m_bufferViews = new object[0];
            m_accessors = new object[0];
            m_materials = new Material[0];
            m_images = new Texture2D[0];
            m_nodes = new GameObject[0];

            finished = false;
            limit = 0;
        }

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

        float DictOptFloat(DataDictionary dict, string fieldName, float defaultValue)
        {
            float retValue = defaultValue;
            if (dict.TryGetValue(fieldName, TokenType.Double, out DataToken doubleToken))
            {
                return (float)(double)doubleToken;
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

        Matrix4x4 DictOptMatrix4x4(DataDictionary dict, string fieldName, Matrix4x4 defaultMatrix)
        {
            Matrix4x4 retValue = defaultMatrix;

            if (dict.TryGetValue(fieldName, TokenType.DataList, out DataToken dataListToken))
            {
                DataList list = (DataList)dataListToken;
                if ((list.Count >= 16) && (IsListComponentType(list, TokenType.Double)))
                {
                    retValue = DataListToMatrix4x4(list);
                }
            }

            return retValue;
        }

        public float IsAlive(float retValue)
        {
            return retValue;
        }

        void NotifyState(string state)
        {
            if (stateReceivers == null) return;

            int nBehaviours = stateReceivers.Length;
            for (int b = 0; b < nBehaviours; b++)
            {
                var receiver = stateReceivers[b];
                if (receiver == null) continue;
                receiver.SendCustomEvent(state);
            }
        }

        void StartParsing()
        {

            currentState = 0;
            enabled = true;
            //ReportInfo("StartParsing", $"Starting at {Time.realtimeSinceStartup}");
            //ParseGLB();
        }

        void Clear()
        {
            NotifyState("SceneCleared");
            ResetState();


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
            //ReportInfo("OnStringLoadSuccess", $"Time : {Time.realtimeSinceStartup}");
            Clear();
            glb = result.ResultBytes;
            StartParsing();
        }

        public override void OnStringLoadError(IVRCStringDownload result)
        {
            ReportError("StringDownloader", $"Error loading string: {result.ErrorCode} - {result.Error}");
        }

        void Start()
        {
            m_accessorTypesInfo = new DataDictionary();
            m_accessorTypesInfo["VEC3"] = 3;
            m_accessorTypesInfo["VEC2"] = 2;
            m_accessorTypesInfo["SCALAR"] = 1;

            // See https://kcoley.github.io/glTF/specification/2.0/
            // 5120 is BYTE
            m_accessorTypesInfo[5120] = 1;
            // 5121 is UNSIGNED_BYTE
            m_accessorTypesInfo[5121] = 1;
            // 5122 is SHORT
            m_accessorTypesInfo[5122] = 2;
            // 5123 is UNSIGNED SHORT
            m_accessorTypesInfo[5123] = 2;
            // 5215 is UNSIGNED INT
            m_accessorTypesInfo[5125] = 4;
            // 5126 is FLOAT
            m_accessorTypesInfo[5126] = 4;

            //DownloadModel();
        }

        void DownloadModel()
        {
            VRCStringDownloader.LoadUrl(userURL, (VRC.Udon.Common.Interfaces.IUdonEventReceiver)this);
        }

        public void UserURLUpdated()
        {
            DownloadModel();
        }

        void DumpList(string name, DataList list)
        {
            Debug.Log($"<color=blue> Dumping list {name} !</color>");
            int nElements = list.Count;
            for (int i = 0; i < nElements; i++)
            {
                if (list.TryGetValue(i, TokenType.String, out DataToken stringValue))
                {
                    //ReportInfo("GLB", $"Key : {(string)stringValue}");
                }
            }
        }

        bool CheckFields(DataDictionary dictionary, params object[] fieldNamesAndTypes)
        {
            bool allFielsAreOk = true;
            int nObjects = fieldNamesAndTypes.Length;
            for (int i = 0; i < nObjects; i += 2)
            {
                string key = (string)fieldNamesAndTypes[i + 0];
                TokenType type = (TokenType)fieldNamesAndTypes[i + 1];
                ReportInfo("CheckFields", $"key is {key.GetType()}");

                bool fieldIsOk = (dictionary.ContainsKey(key) && dictionary[key].TokenType == type);
                allFielsAreOk &= fieldIsOk;
            }
            return allFielsAreOk;
        }

        ushort[] GetUshorts(byte[] glbData, int offset, int nBytes)
        {
            int nUshorts = nBytes / 2;
            ushort[] ret = new ushort[nUshorts];
            System.Buffer.BlockCopy(glbData, offset, ret, 0, nBytes);
            return ret;
        }

        int[] GetUints(byte[] glbData, int offset, int nBytes)
        {
            int nUints = nBytes / 4;
            int[] ret = new int[nUints];
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

        float[] GetStridedFloats(byte[] glbData, int offset, int readSizeInBytes, int strideInBytes)
        {
            /* This is one of the worse scenario.
             * If you do this in your GLB file, FUCK YOU !
             */
            int nFloats = readSizeInBytes / 4;
            float[] ret = new float[nFloats];
            for (int f = 0, cursor = offset; f < nFloats; f++, cursor += strideInBytes)
            {
                ret[f] = System.BitConverter.ToSingle(glbData, cursor);
            }
            return ret;
        }

        int[] GetStridedInts(byte[] glbData, int offset, int readSizeInBytes, int strideInBytes)
        {
            int nUints = readSizeInBytes / 4;
            int[] ret = new int[nUints];
            for (int u = 0, cursor = offset; u < nUints; u++, cursor += strideInBytes)
            {
                ret[u] = System.BitConverter.ToInt32(glbData, cursor);
            }
            return ret;
        }

        ushort[] GetStridedUshorts(byte[] glbData, int offset, int readSizeInBytes, int strideInBytes)
        {
            int nUshorts = readSizeInBytes / 2;
            ushort[] ret = new ushort[nUshorts];
            for (int u = 0, cursor = offset; u < nUshorts; u++, cursor += strideInBytes)
            {
                ret[u] = System.BitConverter.ToUInt16(glbData, cursor);
            }
            return ret;
        }

        Matrix4x4[] FloatsToMatrix4x4(float[] floats, int nFloats, int byteStride)
        {
            /* The default 'stride' is 16.
             * 
             * Meaning that :
             * - We start at some specific point
             * - We read 16 floats from there
             *   and fill a Matrix4x4 with it
             * - Then from this point, we jump 16 floats forward
             * 
             * Which is just a complicated way of saying,
             * "the data are packed and just read everything in one time".
             * 
             * However, there MIGHT be a stride SUPERIOR to 16 floats.
             * In which case, we round it to a 'float' and consider it
             * when jumping from one point to the next one.
             */
            int floatStride = 16;
            if (byteStride > 16*4)
            {
                floatStride = byteStride / 4;
            }
            int nMatrices = nFloats / 16;

            Matrix4x4[] ret = new Matrix4x4[nMatrices];
            for (int m = 0, f = 0; m < nMatrices; m++, f += floatStride)
            {
                ret[m][0]  = floats[f + 0];
                ret[m][1]  = floats[f + 1];
                ret[m][2]  = floats[f + 2];
                ret[m][3]  = floats[f + 3];

                ret[m][4]  = floats[f + 4];
                ret[m][5]  = floats[f + 5];
                ret[m][6]  = floats[f + 6];
                ret[m][7]  = floats[f + 7];

                ret[m][8]  = floats[f + 8];
                ret[m][9]  = floats[f + 9];
                ret[m][10] = floats[f + 10];
                ret[m][11] = floats[f + 11];

                ret[m][12] = floats[f + 12];
                ret[m][13] = floats[f + 13];
                ret[m][14] = floats[f + 14];
                ret[m][15] = floats[f + 15];
            }
            return ret;
        }

        Vector3[] FloatsToVector3(float[] floats, int nFloats, int byteStride)
        {
            int floatStride = 3;
            if (byteStride > 12)
            {
                floatStride = byteStride / 4;
            }
            int nVectors = nFloats / 3;
            Vector3[] ret = new Vector3[nVectors];
            for (int v = 0, f = 0; v < nVectors; v++, f += floatStride)
            {
                ret[v].x = floats[f + 0];
                ret[v].y = floats[f + 1];
                ret[v].z = floats[f + 2];
            }
            return ret;
        }

        Vector3[] RescaleVector3(Vector3[] vectors, Vector3 scale)
        {
            int nVectors = vectors.Length;
            for (int v = 0, f = 0; v < nVectors; v++, f += 3)
            {
                Vector3 vec = vectors[v];
                vec.x *= scale.x;
                vec.y *= scale.y;
                vec.z *= scale.z;
                vectors[v] = vec;
            }
            return vectors;
        }

        Vector2[] FloatsToVector2(float[] floats, int nFloats, int byteStride)
        {
            int floatStride = 2;
            if (byteStride > 8)
            {
                floatStride = byteStride / 4;
            }
            int nVectors = nFloats / 2;
            Vector2[] ret = new Vector2[nVectors];
            for (int v = 0, f = 0; v < nVectors; v++, f += floatStride)
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

        ushort[] InvertTriangles(ushort[] indices)
        {
            int nTriangles = indices.Length / 3;
            int i, b, c;
            for (int t = 0; t < nTriangles; t++)
            {
                i = t * 3;
                // a = i + 0;
                b = i + 1;
                c = i + 2;

                ushort pointB = indices[b];
                indices[b] = indices[c];
                indices[c] = pointB;
            }
            return indices;
        }

        int[] InvertTriangles(int[] indices)
        {
            int nTriangles = indices.Length / 3;
            int i, b, c;
            for (int t = 0; t < nTriangles; t++)
            {
                i = t * 3;
                // a = i + 0;
                b = i + 1;
                c = i + 2;

                int pointB = indices[b];
                indices[b] = indices[c];
                indices[c] = pointB;
            }
            return indices;
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

        Color DataListToColorRGB(DataList list)
        {
            return new Color((float)(double)list[0], (float)(double)list[1], (float)(double)list[2]);
        }

        Matrix4x4 DataListToMatrix4x4(DataList list)
        {
            return new Matrix4x4(
                new Vector4((float)(double)list[0], (float)(double)list[1], (float)(double)list[2], (float)(double)list[3]),
                new Vector4((float)(double)list[4], (float)(double)list[5], (float)(double)list[6], (float)(double)list[7]),
                new Vector4((float)(double)list[8], (float)(double)list[9], (float)(double)list[10], (float)(double)list[11]),
                new Vector4((float)(double)list[12], (float)(double)list[13], (float)(double)list[14], (float)(double)list[15])
                );
        }

        int ParseAccessors(int startFrom)
        {
            if (glbJson == null)
            {
                return errorValue;
            }
            
            if (startFrom == 0)
            {
                bool gotAccessors = glbJson.TryGetValue("accessors", TokenType.DataList, out DataToken accessorsListToken);
                if (!gotAccessors)
                {
                    m_accessors = new object[0];
                    return sectionComplete;
                }
            }

            DataList accessorsList = (DataList)glbJson["accessors"];
            int nAccessors = accessorsList.Count;

            if (startFrom == 0)
            {
                m_accessors = new object[nAccessors];
            }

            object[] accesssors = m_accessors;
            for (int i = startFrom; i < nAccessors; i++)
            {
                if ((i != startFrom) & (!StillHaveTime()))
                {
                    return i;
                }

                DataToken accessorToken = accessorsList[i];
                if (accessorToken.TokenType != TokenType.DataDictionary)
                {
                    continue;
                }
                accesssors[i] = ParseAccessor((DataDictionary)accessorToken);

            }
            return sectionComplete;
        }

        Vector3 invalidVector = new Vector3(float.NaN, float.NaN, float.NaN);

        object[] ParseAccessor(DataDictionary accessorInfo)
        {
            object[] accessor = new object[accessorFieldsCount];

            accessor[accessorFieldBufferParsed] = false;
            accessor[accessorFieldBufferReference] = null;
            accessor[accessorFieldBufferBufferView] = DictOptInt(accessorInfo, "bufferView", -1);
            accessor[accessorFieldBufferOffset] = DictOptInt(accessorInfo, "byteOffset", 0);
            accessor[accessorFieldBufferCount] = DictOptInt(accessorInfo, "count", 0);
            accessor[accessorFieldBufferComponentType] = DictOptInt(accessorInfo, "componentType", 0);
            accessor[accessorFieldBufferType] = DictOptString(accessorInfo, "type", "_UNKNOWN_");

            return accessor;
        }
        const int accessorBufferIndex = 0;
        const int accessorComponentTypeIndex = 1;
        const int accessorCountIndex = 2;
        const int accessorTypeIndex = 3;

        bool GetSubmeshInfo(DataDictionary primitives, out int positionsAccessor, out int normalsAccessor, out int uvsAccessor, out int indicesAccessor, out int materialIndex)
        {
            positionsAccessor = -1;
            normalsAccessor = -1;
            uvsAccessor = -1;
            indicesAccessor = -1;
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
                "POSITION", TokenType.Double);
            if (!check)
            {
                ReportError("Getsubmeshinfo", $"Invalid attributes in {primitives}");
                return check;
            }



            positionsAccessor = (int)((double)attributes["POSITION"]);
            normalsAccessor = DictOptInt(attributes, "NORMAL", -1);
            indicesAccessor = (int)((double)primitives["indices"]);

            uvsAccessor = DictOptInt(attributes, "TEXCOORD_0", -1);
            //ReportInfo("GetSubmeshInfo", $"TEXCOORD_0 : {uvsView}");
            materialIndex = DictOptInt(primitives, "material", -1);
            return true;
        }

        const int meshInfoPositionAccessorIndex = 0;
        const int meshInfoNormalsAccessorIndex = 1;
        const int meshInfoUvsAccessorIndex = 2;
        const int meshInfoIndicesAccessorIndex = 3;
        const int meshInfoNIndices = 4;
        bool GetMeshInfo(DataDictionary meshInfo, out string name, out int meshes, out int[] views, out int[] materialsIndices)
        {
            name = DictOptString(meshInfo, "name", "_GLBLoader_AnonymousMesh");
            meshes = 0;
            views = new int[0];
            materialsIndices = new int[0];

            bool check = CheckFields(
                meshInfo,
                "primitives", TokenType.DataList);
            if (!check)
            {
                ReportError("GetMeshInfo", "No primitives Dictionary in this Mesh Info");
                return check;
            }

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

                views[v + meshInfoPositionAccessorIndex] = positionsView;
                views[v + meshInfoNormalsAccessorIndex] = normalsView;
                views[v + meshInfoUvsAccessorIndex] = uvsView;
                views[v + meshInfoIndicesAccessorIndex] = indicesView;
                materialsIndices[m] = materialIndex;
                //ReportInfo("GetMeshInfo", $"{name} : Mesh {m} - {positionsView},{normalsView},{uvsView},{indicesView},{materialIndex}");
                v += meshInfoNIndices;
                actualNumberOfMeshes += 1;
            }
            meshes = actualNumberOfMeshes;

            return true;

        }


        object GetFloatBuffer(int offset, int bufferSize, int readSize, int stride, string accessorType)
        {
            if (accessorType == "SCALAR" && stride > 4)
            {
                return GetStridedFloats(glb, offset, readSize, stride);
            }
            object buffer = GetFloats(glb, offset, bufferSize);
            int nFloats = readSize / 4;
            switch (accessorType)
            {
                case "VEC2":
                    buffer = FloatsToVector2((float[])buffer, nFloats, stride);
                    break;

                case "VEC3":
                    buffer = FloatsToVector3((float[])buffer, nFloats, stride);
                    break;

                case "MAT4":
                    buffer = FloatsToMatrix4x4((float[])buffer, nFloats, stride);
                    break;
            }
            return buffer;
        }

        object GetIntsBuffer(int offset, int bufferSize, int readSize, int stride, string accessorType)
        {
            if (accessorType == "SCALAR" && stride > 4)
            {
                return GetStridedInts(glb, offset, bufferSize, stride);
            }

            return GetUints(glb, offset, readSize);
        }

        object GetUshortBuffer(int offset, int bufferSize, int readSize, int stride, string accessorType)
        {
            if (accessorType == "SCALAR" && stride > 2)
            {
                return GetStridedUshorts(glb, offset, bufferSize, stride); 
            }

            return GetUshorts(glb, offset, readSize);
        }

        const int rescaleOptionIndex = 0;
        const int scaleFactorOptionIndex = 1;
        const int alignOn3OptionIndex = 2;
        const int invertTrianglesOptionIndex = 3;
        const int nOptionIndices = 4;

        void ResetAccessorBufferParseOptions(object[] options)
        {
            options[rescaleOptionIndex] = false;
            options[scaleFactorOptionIndex] = Vector3.one;
            options[alignOn3OptionIndex] = false;
            options[invertTrianglesOptionIndex] = false;
        }

        object[] AccessorBufferParseOptions()
        {
            object[] options = new object[nOptionIndices];

            ResetAccessorBufferParseOptions(options);

            return options;
        }

        object ParseAccessorBuffer(object[] accessor, object[] options)
        {
            int bufferView = (int)accessor[accessorFieldBufferBufferView];
            if ((bufferView < 0) | (bufferView >= m_bufferViews.Length))
            {
                return null;
            }
            int[] bufferInfo = (int[])m_bufferViews[bufferView];
            if (bufferInfo == null)
            {
                return null;
            }
            int accessorComponentType = (int)accessor[accessorFieldBufferComponentType];
            int accessorOffset = (int)accessor[accessorFieldBufferOffset];
            int accessorCount = (int)accessor[accessorFieldBufferCount];
            string accessorType = (string)accessor[accessorFieldBufferType];
            int bufferOffset = bufferInfo[bufferViewFieldOffset];
            int bufferSize = bufferInfo[bufferViewFieldSize];
            int byteStride = bufferInfo[bufferViewFieldStride];

            if (!m_accessorTypesInfo.ContainsKey(accessorType))
            {
                return null;
            }
            if (!m_accessorTypesInfo.ContainsKey(accessorComponentType))
            {
                return null;
            }

            int nComponents = (int)m_accessorTypesInfo[accessorType];
            // FIXME : Hack to get around accessors defining a non multiple of 3
            // when trying to get triangles points...
            // Like... If you do this, fuck you really...
            // A triangle is 3 POINTS. Not 2, Not 1. 3.
            bool alignOn3 = (bool)options[invertTrianglesOptionIndex];
            if (alignOn3)
            {
                accessorCount = (accessorCount / 3) * 3;
            }
            
            int componentsSize = (int)m_accessorTypesInfo[accessorComponentType];
            int arrayElementSize = nComponents * componentsSize;
            
            

            int actualOffset = glbDataStart + bufferOffset + accessorOffset;
            int readSizeInBytes = arrayElementSize * accessorCount;
            if (readSizeInBytes > bufferSize) readSizeInBytes = bufferSize;

            ReportInfo("ParseAccessorBuffer", $"ByteStride : {byteStride}, natural stride : {arrayElementSize} - Read {readSizeInBytes} of {bufferSize}");
            int actualStride = (byteStride == 0) ? arrayElementSize : byteStride;

            object buffer;

            /* There's two problems to solve here :
             * - Get the raw data
             * - Store it into the proper Array type, while respecting the Stride.
             * 
             * The solution used here consist in reading the WHOLE buffer and then
             * reading the "actual size in bytes" while respecting the stride, when
             * generating the Vec2/Vec3 arrays.
             * 
             * The whole idea is that, if data are packed together, then we'll
             * be basically reading the same amount of data generally.
             * 
             * However, if there's some weird stride, we'll still have the whole
             * buffer available to read when making the array.
             * 
             * The main issue is that we can easily overshoot and store too much
             * data for scalar data, but I'll take a bit more memory in order
             * to play 'safe' we'll say.
             */

            bool invertTriangles = (bool)options[invertTrianglesOptionIndex];

            switch (accessorComponentType)
            {
                case 5126:
                    buffer = GetFloatBuffer(actualOffset, bufferSize, readSizeInBytes, actualStride, accessorType);
                    break;
                case 5123:
                    buffer = GetUshortBuffer(actualOffset, bufferSize, readSizeInBytes, actualStride, accessorType);
                    if (invertTriangles)
                    {
                        buffer = InvertTriangles((ushort[])buffer);
                    }
                    break;
                case 5125:

                    buffer = GetIntsBuffer(actualOffset, bufferSize, readSizeInBytes, actualStride, accessorType);
                    if (invertTriangles)
                    {
                        buffer = InvertTriangles((int[])buffer);
                    }
                    break;
                default:
                    ReportError("ParseAccessorBuffer", $"Unhandled Component type {accessorComponentType} !");
                    return null;
            }

            bool rescale = (bool)options[rescaleOptionIndex];
            if (rescale)
            {
                Vector3 scaleFactor = (Vector3)options[scaleFactorOptionIndex];
                RescaleVector3((Vector3[])buffer, scaleFactor);
            }

            accessor[accessorFieldBufferReference] = buffer;
            accessor[accessorFieldBufferParsed] = true;
            return buffer;
        }

        object GetAccessorBuffer(int accessorIndex, object[] options)
        {
            if (accessorIndex < 0)
            {
                return null;
            }

            if ((accessorIndex >= m_accessors.Length))
            {
                ReportError("GetAccessorBuffer", $"accessorIndex is out of bounds : {accessorIndex}:{m_accessors.Length - 1}");
                return null;
            }

            object[] accessor = (object[])m_accessors[accessorIndex];
            if (accessor == null)
            {
                ReportError("GetAccessorBuffer", $"No accessor info !");
                return null;
            }
            
            if (accessor.Length < accessorFieldsCount)
            {
                ReportError("GetAccessorBuffer", $"Not enough info in accessors {accessor.Length} < {accessorFieldsCount}");
                return null;
            }

            bool bufferParsed = (bool)accessor[accessorFieldBufferParsed];
            if (bufferParsed)
            {
                return accessor[accessorFieldBufferReference];
            }

            return ParseAccessorBuffer(accessor, options);
        }

        System.Type vector3Array = typeof(Vector3[]);
        System.Type ushortArray = typeof(ushort[]);
        System.Type vector2Array = typeof(Vector2[]);
        System.Type intArray = typeof(int[]);

        Mesh LoadMeshFrom(int[] meshInfo, int startOffset)
        {
            Mesh m = new Mesh();

            int positionsAccessorIndex = meshInfo[startOffset + meshInfoPositionAccessorIndex];
            int normalsAccessorIndex = meshInfo[startOffset + meshInfoNormalsAccessorIndex];
            int uvsAccessorIndex = meshInfo[startOffset + meshInfoUvsAccessorIndex];
            int indicesAccessorIndex = meshInfo[startOffset + meshInfoIndicesAccessorIndex];

            object[] parseOptions = AccessorBufferParseOptions();
            parseOptions[rescaleOptionIndex] = true;
            parseOptions[scaleFactorOptionIndex] = new Vector3(-1, 1, 1);

            object positionsBuffer = GetAccessorBuffer(positionsAccessorIndex, parseOptions);
            if (positionsBuffer == null)
            {
                ReportError("LoadMeshFrom", "Invalid Positions Accessor");
                return m;
            }

            ResetAccessorBufferParseOptions(parseOptions);
            parseOptions[invertTrianglesOptionIndex] = true;

            object indicesBuffer = GetAccessorBuffer(indicesAccessorIndex, parseOptions);
            if (indicesBuffer == null)
            {
                ReportError("LoadMeshFrom", "Invalid Indices Accessor");
                return m;
            }


            if ((positionsBuffer.GetType() != vector3Array) | ((indicesBuffer.GetType() != ushortArray) && (indicesBuffer.GetType() != intArray)))
            {
                ReportError("LoadMesh", $"Some buffer views types are invalid : {positionsBuffer.GetType()}, {indicesBuffer.GetType()}");
                return m;
            }

            m.vertices = (Vector3[])positionsBuffer;

            ResetAccessorBufferParseOptions(parseOptions);
            parseOptions[rescaleOptionIndex] = true;
            parseOptions[scaleFactorOptionIndex] = new Vector3(-1, 1, 1);
            object normalsBuffer = GetAccessorBuffer(normalsAccessorIndex, parseOptions);
            if (normalsBuffer != null && normalsBuffer.GetType() == vector3Array)
            {
                m.normals = (Vector3[])normalsBuffer;
            }

            ResetAccessorBufferParseOptions(parseOptions);
            object uvsBuffer = GetAccessorBuffer(uvsAccessorIndex, parseOptions);

            //ReportInfo("LoadMeshFrom", "Got an accessor !");
            if (uvsBuffer != null && uvsBuffer.GetType() == vector2Array)
            {
                //ReportInfo("LoadMeshFrom", "Setting up the UVS !");
                m.uv = (Vector2[])uvsBuffer;
            }

            if (indicesBuffer.GetType() == ushortArray)
            {
                ushort[] indices = (ushort[])indicesBuffer;
                ReportInfo("LoadMeshFrom", $"Indices : {indices.Length} (% 3 ? {indices.Length % 3})");
                m.SetIndices(indices, MeshTopology.Triangles, 0);
            }
            else if (indicesBuffer.GetType() == intArray)
            {
                int[] indices = (int[])indicesBuffer;
                m.SetIndices(indices, MeshTopology.Triangles, 0);
            }

            if (normalsBuffer == null)
            {
                m.RecalculateNormals();
            }
            
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

            //ReportInfo("LoadMesh", $"$Mesh : {name} nSubmeshes : {nSubmeshes}");

            int indicesSum = 0;
            CombineInstance[] instances = new CombineInstance[nSubmeshes];
            for (int s = 0; s < nSubmeshes; s++)
            {
                CombineInstance instance = instances[s];
                instance.transform = Matrix4x4.identity;
                instance.mesh = LoadMeshFrom(submeshesInfo, s * meshInfoNIndices);
                instance.subMeshIndex = 0;
                indicesSum += instance.mesh.vertexCount;
                instances[s] = instance;
            }

            ReportInfo("LoadMesh", $"Indices : {indicesSum}");
            mesh.indexFormat = (indicesSum < 65535) ? UnityEngine.Rendering.IndexFormat.UInt16 : UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.CombineMeshes(instances, false);
            return true;
        }

        int[] GetBufferViewInfo(DataDictionary bufferViewInfo)
        {
            int[] bufferView = new int[bufferViewFieldsCount];
            bufferView[bufferViewFieldBufferIndex] = DictOptInt(bufferViewInfo, "buffer", 0);
            bufferView[bufferViewFieldOffset] = DictOptInt(bufferViewInfo, "byteOffset", 0);
            bufferView[bufferViewFieldSize] = DictOptInt(bufferViewInfo, "byteLength", 0);
            bufferView[bufferViewFieldTarget] = DictOptInt(bufferViewInfo, "target", 0);
            bufferView[bufferViewFieldStride] = DictOptInt(bufferViewInfo, "byteStride", 0);
            return bufferView;
        }

        int ParseBufferViews(int startFrom)
        {
            if (glbJson == null)
            {
                return errorValue;
            }

            if (startFrom == 0)
            {
                bool gotBufferViews = glbJson.TryGetValue("bufferViews", TokenType.DataList, out DataToken bufferViewsToken);
                if (!gotBufferViews)
                {
                    m_bufferViews = new object[0];
                    return sectionComplete;
                }
            }

            DataList bufferViews = (DataList)glbJson["bufferViews"];
            
            if (startFrom == 0)
            {
                m_bufferViews = new object[bufferViews.Count];
            }

            object[] views = m_bufferViews;
            int nViews = m_bufferViews.Length;
            
            for (int v = startFrom; v < nViews; v++)
            {
                DataToken bufferViewToken = bufferViews[v];
                if (bufferViewToken.TokenType != TokenType.DataDictionary)
                {
                    views[v] = null;
                }
                DataDictionary bufferView = (DataDictionary)bufferViewToken;
                views[v] = GetBufferViewInfo(bufferView);
            }
            return sectionComplete;
        }

        int ParseMeshes(int startFrom)
        {
            if (glbJson == null)
            {
                return errorValue;
            }
            if (startFrom == 0)
            {
                bool hasMeshes = glbJson.TryGetValue("meshes", TokenType.DataList, out DataToken meshesListToken);
                if (!hasMeshes)
                {
                    m_meshesInfo = new object[0];
                    return sectionComplete;
                }
            }

            DataList meshesList = (DataList)glbJson["meshes"];
            int nMeshes = meshesList.Count;

            if (startFrom == 0)
            {
                m_meshesInfo = new object[nMeshes];
            }

            object[] meshesInfo = m_meshesInfo;

            for (int m = startFrom; m < nMeshes; m++)
            {
                DataToken meshInfoToken = meshesList[m];
                if (meshInfoToken.TokenType != TokenType.DataDictionary) continue;

                bool gotAMesh = LoadMesh((DataDictionary)meshInfoToken, out string name, out Mesh mesh, out int[] materialsIndices);
                if (!gotAMesh) continue;

                meshesInfo[m] = new object[] { mesh, materialsIndices };
                
                if ((m != startFrom) & (!StillHaveTime()))
                {
                    return m;
                }
            }
            return sectionComplete;
        }

        bool ParseNode(DataDictionary node, out int meshIndex, out string name, out Vector3 position, out Quaternion rotation, out Vector3 scale, out int[] children)
        {
            meshIndex = DictOptInt(node, "mesh", -1);
            name = DictOptString(node, "name", "");
            if (node.ContainsKey("matrix"))
            {
                Matrix4x4 matrix = DictOptMatrix4x4(node, "matrix", Matrix4x4.identity);
                position = matrix.GetPosition();
                rotation = matrix.rotation;
                scale = matrix.lossyScale;
            }
            else
            {
                position = DictOptVector3(node, "translation", Vector3.zero);
                rotation = DictOptQuaternion(node, "rotation", Quaternion.identity);
                scale = DictOptVector3(node, "scale", Vector3.one);
            }

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
            mesh.RecalculateBounds();

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
                    sharedMaterials[sharedMatIndex] = NewMaterial(baseMaterial);
                }
            }
            renderer.sharedMaterials = sharedMaterials;

            BoxCollider boxCollider = node.GetComponent<BoxCollider>();
            Vector3 center = renderer.bounds.center;
            center.x *= -1;
            center.z *= -1;
            boxCollider.center = center;
            boxCollider.size = renderer.bounds.size;
        }

        int SpawnNodes(int startFrom)
        {
            if (glbJson == null)
            {
                return errorValue;
            }
            
            if (startFrom == 0)
            {
                bool hasNodes = glbJson.TryGetValue("nodes", TokenType.DataList, out DataToken nodeslistToken);
                if (!hasNodes)
                {
                    return sectionComplete;
                }
            }

            DataList nodesList = (DataList)glbJson["nodes"];
            int nNodes = nodesList.Count;

            if (startFrom == 0)
            {
                m_nodes = new GameObject[nNodes];
            }

            GameObject[] nodesObjects = m_nodes;
            for (int n = startFrom; n < nNodes; n++)
            {
                if ((n != startFrom) & (!StillHaveTime()))
                {
                    return n;
                }
                nodesObjects[n] = Instantiate(nodePrefab, mainParent);
            }

            return sectionComplete;
        }

        int SetupNodes(int startFrom)
        {
            if (glbJson == null)
            {
                return errorValue;
            }

            if (startFrom == 0)
            {
                if (m_nodes == null)
                {
                    return sectionComplete;
                }

                if (m_nodes.Length == 0)
                {
                    return sectionComplete;
                }
            }

            DataList nodesList = (DataList)glbJson["nodes"];
            int nNodes = nodesList.Count;

            int maxMeshIndex = m_meshesInfo.Length - 1;
            GameObject[] nodesObjects = m_nodes;
            for (int n = startFrom; n < nNodes; n++)
            {
                if ((n != startFrom) & (!StillHaveTime()))
                {
                    return n;
                }
                DataToken nodeToken = nodesList[n];
                if (nodeToken.TokenType != TokenType.DataDictionary) continue;
                ParseNode((DataDictionary)nodeToken, out int meshIndex, out string name, out Vector3 position, out Quaternion rotation, out Vector3 scale, out int[] children);

                GameObject node = nodesObjects[n];

                SetupMesh(node, meshIndex, maxMeshIndex);

                node.name = name;

                position.x *= -1;
                rotation = new Quaternion(-rotation.x, rotation.y, rotation.z, -rotation.w);

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

            return sectionComplete;

        }

        Material NewMaterial(Material templateMaterial)
        {
            /* This actually Instantiate a new material.
             * For some reason, you can't do Instantiate(material)
             * with Udon
             */
            temporaryRenderer.material = templateMaterial;

            return temporaryRenderer.material;
        }

        // Shamelessly stolen from https://forum.unity.com/threads/standard-material-shader-ignoring-setfloat-property-_mode.344557/
        public static void MaterialSetAsFade(Material material)
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

        public static void MaterialSetAsCutout(Material material, float threshold)
        {
            Debug.Log("Setting material as Cutout !");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.SetFloat("_Cutoff", threshold);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.EnableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 2450;
        }

        Texture2D MaterialInfoGetTextureIfAvailable(DataDictionary info, string textureKey, out DataDictionary outTextureInfo)
        {

            outTextureInfo = null;
            bool textureKeyExist = info.TryGetValue(textureKey, TokenType.DataDictionary, out DataToken textureInfoToken);
            if (!textureKeyExist) return null;

            outTextureInfo = (DataDictionary)textureInfoToken;

            DataDictionary textureInfo = (DataDictionary)textureInfoToken;
            bool hasIndex = textureInfo.TryGetValue("index", TokenType.Double, out DataToken indexToken);

            if (!hasIndex) return null;

            int textureIndex = (int)(double)indexToken;

            if ((textureIndex < 0) | (textureIndex >= m_images.Length)) return null;

            return m_images[textureIndex];
        }

        Material CreateMaterialFrom(DataDictionary materialInfo)
        {
            Material mat = NewMaterial(baseMaterial);
            bool isUnlit = false;
            if (materialInfo.TryGetValue("extensions", TokenType.DataDictionary, out DataToken extensionsDictToken))
            {
                DataDictionary extensionsDict = (DataDictionary)extensionsDictToken;
                isUnlit = extensionsDict.ContainsKey("KHR_materials_unlit");
                if (isUnlit)
                {
                    mat.shader = unlitShader;
                }
            }
            mat = (mat == null) ? NewMaterial(baseMaterial) : mat;

            mat.name = DictOptString(materialInfo, "name", mat.name);

            Texture2D normalTexture = MaterialInfoGetTextureIfAvailable(materialInfo, "normalTexture", out DataDictionary normalTextureInfo);
            if (normalTexture != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                // Gotta love the coherency here...
                mat.SetTexture("_BumpMap", normalTexture);
                mat.SetFloat("_BumpScale", DictOptFloat(normalTextureInfo, "scale", 1.0f));
            }

            string alphaMode = DictOptString(materialInfo, "alphaMode", "");
            /* FIXME Handle different Alpha modes correctly ! */
            if (alphaMode != "")
            {
                if (alphaMode == "BLEND")
                {
                    if (isUnlit)
                    {
                        mat.shader = unlitShaderTransparent;
                    }
                    MaterialSetAsFade(mat);
                }
                else if (alphaMode == "MASK")
                {
                    if (isUnlit)
                    {
                        mat.shader = unlitShaderCutout;
                    }
                    MaterialSetAsCutout(mat, DictOptFloat(materialInfo, "alphaCutoff", 0.5f));
                }
            }

            if (materialInfo.TryGetValue("pbrMetallicRoughness", TokenType.DataDictionary, out DataToken pbrToken))
            {
                DataDictionary pbrInfo = (DataDictionary)pbrToken;
                Texture2D albedoTexture = MaterialInfoGetTextureIfAvailable(pbrInfo, "baseColorTexture", out DataDictionary baseColorTexInfo);
                if (albedoTexture != null) mat.SetTexture("_MainTex", albedoTexture);

                if (pbrInfo.TryGetValue("baseColorFactor", TokenType.DataList, out DataToken colorToken))
                {
                    DataList colorInfo = (DataList)colorToken;
                    if (colorInfo.Count == 4 && IsListComponentType(colorInfo, TokenType.Double))
                    {
                        Color color = DataListToColor(colorInfo);
                        mat.color = color;
                        if (color.a < 1)
                        {
                            MaterialSetAsFade(mat);
                        }
                    }
                }
                if (pbrInfo.TryGetValue("metallic", TokenType.Double, out DataToken metallicToken))
                {
                    mat.SetFloat("_Metallic", (float)(double)metallicToken);
                }



                Texture2D metallicRoughnessTexture = MaterialInfoGetTextureIfAvailable(pbrInfo, "metallicRoughnessTexture", out DataDictionary metalRoughTexInfo);
                if (metallicRoughnessTexture != null) mat.SetTexture("_MetallicGlossMap", metallicRoughnessTexture);

                float roughness = DictOptFloat(pbrInfo, "roughnessFactor", 1f);
                mat.SetFloat("_Glossiness", 1- roughness);
                mat.SetFloat("_GlossMapScale", 1 - roughness);
            }

            if (materialInfo.TryGetValue("emissiveFactor", TokenType.DataList, out DataToken emissionColorToken))
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                mat.SetColor("_EmissionColor", DataListToColorRGB((DataList)emissionColorToken));
            }

            Texture2D emissionTexture = MaterialInfoGetTextureIfAvailable(materialInfo, "emissiveTexture", out DataDictionary emissiveTextureInfo);
            if (emissionTexture != null)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                mat.SetTexture("_EmissionMap", emissionTexture);
            }

            

            // Let's forget about Double side for the moment...
            return mat;
        }

        int ParseMaterials(int startFrom)
        {
            if (glbJson == null)
            {
                return errorValue;
            }

            if (startFrom == 0)
            {
                bool hasMaterials = glbJson.TryGetValue("materials", TokenType.DataList, out DataToken materialsListToken);
                if (!hasMaterials)
                {
                    m_materials = new Material[0];
                    return sectionComplete;
                }
            }

            DataList materialsList = (DataList)glbJson["materials"];
            int nMaterials = materialsList.Count;

            if (startFrom == 0)
            {
                m_materials = new Material[nMaterials];
            }

            Material[] materials = m_materials;
            for (int m = startFrom; m < nMaterials; m++)
            {
                if ((m != startFrom) & (!StillHaveTime()))
                {
                    return m;
                }
                DataToken materialInfoToken = materialsList[m];
                if (materialInfoToken.TokenType != TokenType.DataDictionary)
                {
                    materials[m] = null;
                    continue;
                }
                materials[m] = CreateMaterialFrom((DataDictionary)materialInfoToken);
            }
            return sectionComplete;
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
                case "DXT5":
                    textureFormat = TextureFormat.DXT5;
                    break;
                case "BC7":
                    textureFormat = TextureFormat.BC7;
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
            int[] bufferView = (int[])m_bufferViews[bufferViewIndex];

            int offset = glbDataStart + bufferView[bufferViewFieldOffset];
            int size = bufferView[bufferViewFieldSize];

            byte[] textureData = new byte[size];
            Buffer.BlockCopy(glb, offset, textureData, 0, size);


            Texture2D tex = new Texture2D(width, height, textureFormat, false);
            tex.name = DictOptString(imageInfo, "name", "Anonymous");
            tex.LoadRawTextureData(textureData);
            tex.Apply(false, false);
            return tex;
        }

        int ParseImages(int startFrom)
        {
            if (glbJson == null)
            {
                return errorValue;
            }
            if (currentIndex == 0)
            {
                bool hasImages = glbJson.TryGetValue("images", TokenType.DataList, out DataToken imagesListToken);
                if (!hasImages)
                {
                    m_images = new Texture2D[0];
                    return sectionComplete;
                }
            }

            DataList imagesList = (DataList)glbJson["images"];
            int nImages = imagesList.Count;

            if (currentIndex == 0)
            {
                m_images = new Texture2D[nImages];
            }

            Texture2D[] textures = m_images;
            for (int i = startFrom; i < nImages; i++)
            {
                if ((i != startFrom) & (!StillHaveTime()))
                {
                    return i;
                }
                DataToken imageInfoToken = imagesList[i];
                if (imageInfoToken.TokenType != TokenType.DataDictionary) continue;
                textures[i] = ParseImage((DataDictionary)imageInfoToken, glb);

            }
            return sectionComplete;
        }


        int ParseMainData(int resumeFromIndex)
        {
            if (glb == null)
            {
                ReportError("GLB", "No data provided actually... The script is bugged !");
                return errorValue;
            }

            if (glb.Length < 32)
            {
                ReportError("GLB", "GLB size is wrong");
                return errorValue;
            }

            int glbCursor = 0;
            uint magic = System.BitConverter.ToUInt32(glb, glbCursor);
            glbCursor += 4;
            if (magic != 0x46546C67)
            {
                ReportError("GLB", $"Wrong magic. Expected 0x46546C67 but got 0x{magic:X8}");
                return errorValue;
            }

            uint version = System.BitConverter.ToUInt32(glb, glbCursor);
            glbCursor += 4;
            //ReportInfo("GLB", $"GLB Version {version}");

            uint size = System.BitConverter.ToUInt32(glb, glbCursor);
            glbCursor += 4;
            //ReportInfo("GLB", $"Size : {size}");

            //ReportInfo("GLB", $"Data size : {glbContent.Length}");

            if (glb.Length < size)
            {
                ReportError("GLB", $"Not enough data in this GLB file ! Expected {size} but got {glb.Length}");
                return errorValue;
            }

            int chunkLength = System.BitConverter.ToInt32(glb, glbCursor);
            glbCursor += 4;
            uint chunkType = System.BitConverter.ToUInt32(glb, glbCursor);
            glbCursor += 4;

            if (chunkLength < 0)
            {
                ReportError("GLB", $"Negative chunk length {chunkLength}");
                return errorValue;
            }

            if (chunkType != 0x4E4F534A)
            {
                ReportError("GLB", $"Expected a JSON Chunk here !");
                return errorValue;
            }

            //ReportInfo("GLB", $"Next chunk : Type : 0x{chunkType:X8} - Size {chunkLength}");

            string jsonData = System.Text.Encoding.UTF8.GetString(glb, glbCursor, chunkLength);
            //ReportInfo("GLB", jsonData);

            glbCursor += chunkLength;

            chunkLength = System.BitConverter.ToInt32(glb, glbCursor);
            glbCursor += 4;
            chunkType = System.BitConverter.ToUInt32(glb, glbCursor);
            glbCursor += 4;

            if (chunkType != 0x004E4942)
            {
                ReportError("GLB", $"Expected a binary chunk here. Got a 0x{chunkType:X8}");
                return errorValue;
            }

            glbDataStart = glbCursor;
            glbJsonRaw = jsonData;
            return sectionComplete;
        }

        void ParseError()
        {
            NotifyState("ParseError");
            enabled = false;
        }

        void ParseComplete()
        {
            NotifyState("SceneLoaded");
            enabled = false;
        }

        

        bool StillHaveTime()
        {
            //ReportInfo("StillHaveTime", $"{Time.realtimeSinceStartup} < {limit}");
            return Time.realtimeSinceStartup < limit;
        }

        void TriggerNextIteration()
        {
            if (finished)
            {
                ParseComplete();
                return;
            }

            if (currentIndex == errorValue)
            {
                ParseError();
                return;
            }

            if (currentIndex == sectionComplete)
            {
                currentState += 1;
                currentIndex = 0;
            }
        }

        int ParseJsonData(int _)
        {
            if ((glbJsonRaw == null) | (glbJsonRaw == ""))
            {
                return errorValue;
            }
            VRCJson.TryDeserializeFromJson(glbJsonRaw, out DataToken result);
            if (result.TokenType != TokenType.DataDictionary)
            {
                ReportError("GLB", "Invalid GLB Json data !");
                return errorValue;
            }

            glbJson = (DataDictionary)result;

            return sectionComplete;
        }

        private void Update()
        {
            ParseGLB();
        }

        public void ParseGLB()
        {
            
            if (!StillHaveTime())
            {
                limit = Time.realtimeSinceStartup + Time.fixedDeltaTime / 2;
            }

            //ReportInfo("ParseGLB", $"CurrentState : {currentState} - Index : {currentIndex}");

            switch (currentState)
            {
                case 0:
                    currentIndex = ParseMainData(currentIndex);
                    break;
                case 1:
                    currentIndex = ParseJsonData(currentIndex);
                    break;
                case 2:
                    currentIndex = ParseBufferViews(currentIndex);
                    break;
                case 3:
                    currentIndex = ParseAccessors(currentIndex);
                    break;
                case 4:
                    currentIndex = ParseImages(currentIndex);
                    break;
                case 5:
                    currentIndex = ParseMaterials(currentIndex);
                    break;
                case 6:
                    currentIndex = ParseMeshes(currentIndex);
                    break;
                case 7:
                    currentIndex = SpawnNodes(currentIndex);
                    break;
                case 8:
                    currentIndex = SetupNodes(currentIndex);
                    break;
                default:
                    finished = true;
                    //enabled = false;
                    break;
            }

            TriggerNextIteration();
            //ReportInfo("ParseGLB", $"Iteration ended at {Time.realtimeSinceStartup}");

            //m_bufferViews = ParseBufferViews((DataList)glbJsonRoot["bufferViews"], glb, cursor);
            //m_accessors = ParseAccessors((DataList)glbJsonRoot["accessors"]);
            /*if (glbJsonRoot.TryGetValue("images", TokenType.DataList, out DataToken imagesToken))
            {
                m_images = ParseImages((DataList)glbJsonRoot["images"], glb);
            }*/
            //m_materials = ParseMaterials((DataList)glbJsonRoot["materials"]);
            
            //ParseAndShowMeshes((DataList)glbJsonRoot["meshes"]);
            //ParseNodes((DataList)glbJsonRoot["nodes"]);
            

        }
    }

}
