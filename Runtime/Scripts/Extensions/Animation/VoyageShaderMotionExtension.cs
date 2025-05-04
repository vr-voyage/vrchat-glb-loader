
using System.IO;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class VoyageShaderMotionExtension : UdonSharpBehaviour
{
    public const int boneIndex0Index = 0;
    public const int weight0Index = 1;
    public const int boneIndex1Index = 2;
    public const int weight1Index = 3;
    public const int boneIndex2Index = 4;
    public const int weight2Index = 5;
    public const int boneIndex3Index = 6;
    public const int weight3Index = 7;

    const int boneInfoSize = 8;

    const int meshIndex = 0;
    const int bonesIndex = 1;
    const int bonesWeights = 2;

    private int[] dummyHumanBones = new int[]
    {
        (int)HumanBodyBones.Chest,         (int)HumanBodyBones.Spine,
        (int)HumanBodyBones.UpperChest,    (int)HumanBodyBones.Chest,
        (int)HumanBodyBones.Neck,          (int)HumanBodyBones.Head,
    };

    int SumSubmeshes(object[] meshesBonesAndWeights)
    {
        int sum = 0;

        int nMeshesBonesAndWeights = meshesBonesAndWeights.Length;
        for (int mbw = 0; mbw < nMeshesBonesAndWeights; mbw++)
        {
            object[] meshBonesAndWeights = (object[])meshesBonesAndWeights[mbw];
            Mesh currentMesh = (Mesh)meshBonesAndWeights[meshIndex];
            sum += currentMesh.subMeshCount;
        }

        return sum;
    }

    int[] Repeat(int value, int length)
    {
        int[] ret = new int[length];

        for (int i = 0; i < length; i++)
        {
            ret[i] = value;
        }
        return ret;
    }

    int[] RetargetBones(Transform[] srcBones, Transform[] dstBones)
    {
        /* Initialize an array of 'srcBones.Length' to -1 */
        int[] boneMap = Repeat(-1, srcBones.Length);

        for (int srcBoneIndex = 0; srcBoneIndex < srcBones.Length; srcBoneIndex++)
        {
            for (Transform boneOrParent = srcBones[srcBoneIndex];
                boneOrParent != null && boneMap[srcBoneIndex] < 0;
                boneOrParent = boneOrParent.parent)
            {
                boneMap[srcBoneIndex] = System.Array.LastIndexOf(dstBones, boneOrParent);
            }
        }
        return boneMap;
    }

    public void RetargetBindposes(
        Transform[] srcBones,
        Transform[] dstBones,
        Matrix4x4[] srcBindposes,
        Matrix4x4[] dstBindposes,
        int[] boneMap)
    {
        for (int k = 0; k < 2; k++)
        {
            for (int i = 0; i < srcBones.Length; i++)
            {
                var j = boneMap[i];
                if (j >= 0 && dstBindposes[j][3, 3] == 0)
                {
                    if (k == 1)
                    {
                        dstBindposes[j] = (dstBones[j].worldToLocalMatrix * srcBones[i].localToWorldMatrix) * srcBindposes[i];
                    }
                    else if (dstBones[j] == srcBones[i])
                    {
                        dstBindposes[j] = srcBindposes[i];
                    }
                }
            }
        }
    }

    void ClearBoneInfo(object[] boneInfo)
    {
        boneInfo[boneIndex0Index] = 0;
        boneInfo[weight0Index] = 0;
        boneInfo[boneIndex1Index] = 0;
        boneInfo[weight1Index] = 0;
        boneInfo[boneIndex2Index] = 0;
        boneInfo[weight2Index] = 0;
        boneInfo[boneIndex3Index] = 0;
        boneInfo[weight3Index] = 0;
    }

    int[] GenerateRange(int size)
    {
        int[] ret = new int[size];
        for (int i = 0; i < size; i++)
        {
            ret[i] = i;
        }
        return ret;
    }

    int[] SortedIndices(Transform[] dstBones, float[] weights)
    {
        int[] indices = new int[dstBones.Length];
        for (int i = 0; i < indices.Length; i++)
        {
            indices[i] = i;
        }

        // Stable sort using bubble sort logic
        for (int i = 0; i < indices.Length - 1; i++)
        {
            for (int j = 0; j < indices.Length - 1 - i; j++)
            {
                // Compare by weights, and maintain the original order if weights are the same
                if (weights[indices[j]] < weights[indices[j + 1]])
                {
                    // Swap the indices if the one on the right has a higher weight
                    int temp = indices[j];
                    indices[j] = indices[j + 1];
                    indices[j + 1] = temp;
                }
            }
        }

        // Take top 4 indices
        int count = Mathf.Min(4, indices.Length);
        int[] bestIndices = new int[count];
        for (int i = 0; i < count; i++)
        {
            bestIndices[i] = indices[i];
        }

        return bestIndices;
    }

    Matrix4x4[] RetargetBoneWeights(
        Transform[] srcBones,
        Transform[] dstBones,
        Matrix4x4[] srcBindposes,
        Matrix4x4[] dstBindposes,
        object[] boneWeights,
        int[] boneMap)
    {
        var vertMatrices = new Matrix4x4[boneWeights.Length];
        for (int v = 0; v < boneWeights.Length; v++)
        {
            var weights = new float[dstBones.Length];
            var srcMatSum = new Matrix4x4();
            var dstMatSum = new Matrix4x4();
            object[] boneInfo = (object[])boneWeights[v];
            for (int boneInfoIndex = 0; boneInfoIndex < 8; boneInfoIndex += 2)
            {

                int boneIndex = (int)boneInfo[boneInfoIndex + 0];
                float boneWeight = (float)boneInfo[boneInfoIndex + 1];

                var retargetedBoneIndex = boneMap[boneIndex];

                if (boneWeight != 0)
                {
                    var srcMat = srcBones[boneIndex].localToWorldMatrix * srcBindposes[boneIndex];
                    var dstMat = dstBones[retargetedBoneIndex].localToWorldMatrix * dstBindposes[retargetedBoneIndex];


                    for (int k = 0; k < 16; k++)
                    {
                        srcMatSum[k] += srcMat[k] * boneWeight;
                        dstMatSum[k] += dstMat[k] * boneWeight;

                    }

                    weights[retargetedBoneIndex] += boneWeight;

                }


            }

            if (srcMatSum != dstMatSum)
            {
                var diffm = dstMatSum.inverse * srcMatSum;
                var diffv = (diffm.GetColumn(0) - new Vector4(1, 0, 0, 0)).sqrMagnitude
                            + (diffm.GetColumn(1) - new Vector4(0, 1, 0, 0)).sqrMagnitude
                            + (diffm.GetColumn(2) - new Vector4(0, 0, 1, 0)).sqrMagnitude
                            + (diffm.GetColumn(3) - new Vector4(0, 0, 0, 1)).sqrMagnitude;
                if (diffv > 1e-8)
                {
                    int boneIndex0 = (int)boneInfo[boneIndex0Index];
                    int boneIndex1 = (int)boneInfo[boneIndex1Index];

                    Transform bone0 = srcBones[boneIndex0];
                    Transform bone1 = srcBones[boneIndex1];

                    string bone0name = bone0 != null ? bone0.name : "null";
                    string bone1name = bone1 != null ? bone1.name : "null";

                    Debug.Log(
                        $"[Retarget] vertex = MAT * vertex, bones == {bone0name}, {bone1name}, {bone0}");
                }

            }

            ClearBoneInfo(boneInfo);

            int[] fourBestWeightIndices = SortedIndices(dstBones, weights);
            int nBestIndices = fourBestWeightIndices.Length;
            for (int i = 0; i < nBestIndices; i++)
            {
                int dstBoneIndex = fourBestWeightIndices[i];
                boneInfo[i * 2 + 0] = dstBoneIndex;
                boneInfo[i * 2 + 1] = weights[dstBoneIndex];
            }

            vertMatrices[v] = srcMatSum == dstMatSum ? Matrix4x4.identity : dstMatSum.inverse * srcMatSum;
        }

        return vertMatrices;
    }

    Matrix4x4[] RetargetBindposesBoneWeights(
        Transform[] srcBones,
        Transform[] dstBones,
        Matrix4x4[] srcBindposes,
        Matrix4x4[] dstBindposes,
        object[] boneWeights)
    {
        int[] boneMap = RetargetBones(srcBones, dstBones);

        // unmap unused srcBones
        var used = new bool[srcBones.Length];
        int nBonesWeights = boneWeights.Length;
        for (int w = 0; w < nBonesWeights; w++)
        {
            object boneWeightW = boneWeights[w];
            object[] boneInfo = (object[])boneWeightW;
            for (int i = 0; i < boneInfoSize; i += 2)
            {
                int boneIndex = (int)boneInfo[i + 0];
                float boneWeight = (float)boneInfo[i + 1];
                if (boneWeight != 0)
                {
                    used[boneIndex] = true;
                }
            }
        }

        for (int i = 0; i < srcBones.Length; i++)
        {
            if (!used[i])
            {
                boneMap[i] = -1;
            }
        }

        // retarget bindposes for mapped srcBones
        RetargetBindposes(srcBones, dstBones, srcBindposes, dstBindposes, boneMap);
        // map unmapped srcBones to first mapped bone
        int defaultBoneIndex = 0;
        int nBones = boneMap.Length;
        for (int i = 0; i < nBones; i++)
        {
            int boneIndex = boneMap[i];
            if (boneIndex >= 0)
            {
                defaultBoneIndex = boneIndex;
                break;
            }
        }

        for (int i = 0; i < boneMap.Length; i++)
        {
            if (boneMap[i] < 0 && used[i])
            {
                boneMap[i] = defaultBoneIndex;
            }
        }
        return RetargetBoneWeights(srcBones, dstBones, srcBindposes, dstBindposes, boneWeights, boneMap);
    }


    static Matrix4x4[] PremultiplyBindPoses(Matrix4x4[] bindPoses, Matrix4x4 objectToRootInverse)
    {
        int nBindPoses = bindPoses.Length;
        Matrix4x4[] newPoses = new Matrix4x4[nBindPoses];

        for (int b = 0; b < nBindPoses; b++)
        {
            newPoses[b] = bindPoses[b] * objectToRootInverse;
        }

        return newPoses;
    }

    Vector4 ClampBoneWeight(object[] boneInfo)
    {
        Vector4 vec4 = new Vector4();
        for (int i = 0; i < 4; i++)
        {
            int boneIndex = (int)boneInfo[i * 2 + 0];
            float boneWeight = (float)boneInfo[i * 2 + 1];
            vec4[i] = boneIndex + boneWeight / 2f;
        }

        return vec4;
    }

    Matrix4x4[] MultiplyMatrices(Matrix4x4[] matrices, Matrix4x4 multiplier)
    {

        int nMatrices = matrices.Length;
        Matrix4x4[] ret = new Matrix4x4[nMatrices];
        for (int m = 0; m < nMatrices; m++)
        {
            ret[m] = matrices[m] * multiplier;
        }
        return ret;
    }

    Vector3[] PremultiplyVertices(Vector3[] vertices, Matrix4x4[] matrices)
    {
        int nVertices = vertices.Length;
        Vector3[] premultipliedVertices = new Vector3[nVertices];
        for (int i = 0; i < nVertices; i++)
        {
            Vector3 vertex = vertices[i];
            premultipliedVertices[i] = matrices[i].MultiplyPoint3x4(vertex);
        }
        return premultipliedVertices;
    }
    Vector3[] PremultiplyNormals(Vector3[] normals, Matrix4x4[] matrices, int nVertices, float vectorScale)
    {
        int nNormals = normals.Length;
        Vector3[] retNormals = new Vector3[Mathf.Max(nNormals, nVertices)];
        for (int i = 0; i < nNormals; i++)
        {
            Vector3 normal = normals[i];
            retNormals[i] = matrices[i].MultiplyVector(normal) / vectorScale;
        }
        return retNormals;
    }

    Vector4[] PremultiplyTangents(
        Vector4[] srcTangents,
        Matrix4x4[] matrices,
        int nVertices,
        float vectorScale)
    {
        int nTangents = srcTangents.Length;
        Vector4[] retTangents = new Vector4[Mathf.Max(nTangents, nVertices)];
        for (int i = 0; i < nTangents; i++)
        {
            Vector4 tangent = srcTangents[i];
            retTangents[i] =
                new Vector4(0, 0, 0, tangent.w)
                + (Vector4)matrices[i].MultiplyVector(tangent)
                / vectorScale;
        }
        return retTangents;
    }

    Vector4[] ClampedBoneWeights(object[] boneWeights)
    {

        int nBonesWeights = boneWeights.Length;
        Vector4[] retBonesWeights = new Vector4[nBonesWeights];
        for (int i = 0; i < nBonesWeights; i++)
        {
            object[] boneWeight = (object[])boneWeights[i];

            int boneIndex0 = (int)boneWeight[boneIndex0Index];
            float weight0 = (float)boneWeight[weight0Index];
            int boneIndex1 = (int)boneWeight[boneIndex1Index];
            float weight1 = (float)boneWeight[weight1Index];
            int boneIndex2 = (int)boneWeight[boneIndex2Index];
            float weight2 = (float)boneWeight[weight2Index];
            int boneIndex3 = (int)boneWeight[boneIndex3Index];
            float weight3 = (float)boneWeight[weight3Index];

            Vector4 clamped = ClampBoneWeight((object[])boneWeights[i]);

            retBonesWeights[i] = clamped;
        }
        return retBonesWeights;
    }

    int AddSubmeshesAsInstances(
        Mesh baseMesh,
        CombineInstance[] instances,
        int currentInstanceIndex)
    {

        int nInstances = instances.Length;
        int nSubmeshes = baseMesh.subMeshCount;
        for (int submeshIndex = 0;
            (submeshIndex < nSubmeshes) & (currentInstanceIndex < nInstances);
            submeshIndex++, currentInstanceIndex++)
        {
            CombineInstance instance = new CombineInstance();
            instance.mesh = baseMesh;
            instance.subMeshIndex = submeshIndex;
            instance.transform = Matrix4x4.identity;
            instances[currentInstanceIndex] = instance;
        }
        return currentInstanceIndex;
    }

    Mesh CloneMesh(Mesh original)
    {
        int nSubmeshes = original.subMeshCount;
        CombineInstance[] combineInstances = new CombineInstance[nSubmeshes];
        AddSubmeshesAsInstances(original, combineInstances, 0);
        Mesh newMesh = new Mesh();
        newMesh.CombineMeshes(combineInstances, false);
        return newMesh;
    }

    Mesh CreatePlayer(
        object[] sources,
        Transform[] mecanimBones,
        Transform skelRoot)
    {
        Mesh resultMesh = new Mesh();
        Matrix4x4[] bindposes = new Matrix4x4[mecanimBones.Length];
        //Color[] colors;

        int nInstancesNeeded = SumSubmeshes(sources);
        int currentInstanceIndex = 0;
        CombineInstance[] combineInstances = new CombineInstance[nInstancesNeeded];

        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

        sw.Start();

        for (int s = 0; s < sources.Length; s++)
        {
            object[] source = (object[])sources[s];
            Mesh srcMesh = (Mesh)source[0];
            Transform[] srcBones = (Transform[])source[1];
            object[] boneWeights = (object[])source[2];

            Mesh sourceMeshClone = CloneMesh(srcMesh);

            Debug.Log(sw.ElapsedMilliseconds);

            Matrix4x4[] srcBindposes = srcMesh.bindposes;

            Matrix4x4 objectToRoot = skelRoot.worldToLocalMatrix * srcBones[0].localToWorldMatrix * srcBindposes[0];
            Matrix4x4[] premultipliedPoses = PremultiplyBindPoses(srcBindposes, objectToRoot.inverse);

            Debug.Log(sw.ElapsedMilliseconds);




            Matrix4x4[] retargetedMatrices = RetargetBindposesBoneWeights(
                srcBones,
                mecanimBones,
                premultipliedPoses,
                bindposes,
                boneWeights);


            Debug.Log(sw.ElapsedMilliseconds);

            Matrix4x4[] vertMatrices = MultiplyMatrices(retargetedMatrices, objectToRoot);
            //Matrix4x4[] vertMatrices = System.Array.ConvertAll(retargetedMatrices
            //        , m => m * objectToRoot); // bake objectToRoot into vertex position
            //var shapeUV = CreateShapeTex(colors, srcMesh, vertMatrices);
            var vectorScale = 1f;

            Debug.Log(sw.ElapsedMilliseconds);

            //vertices.AddRange(srcMesh.vertices.Select((v, i) => vertMatrices[i].MultiplyPoint3x4(v)));
            int nVertices = sourceMeshClone.vertices.Length;
            Vector3[] vertices = PremultiplyVertices(sourceMeshClone.vertices, vertMatrices);

            Debug.Log(sw.ElapsedMilliseconds);

            //normals.AddRange(srcMesh.normals.Select((v, i) => vertMatrices[i].MultiplyVector(v) / vectorScale));
            int nNormals = sourceMeshClone.normals.Length;
            Vector3[] normals = PremultiplyNormals(sourceMeshClone.normals, vertMatrices, nVertices, vectorScale);

            Debug.Log(sw.ElapsedMilliseconds);

            // tangents.AddRange(srcMesh.tangents.Select((v, i) =>
            //   new Vector4(0, 0, 0, v.w) + (Vector4)vertMatrices[i].MultiplyVector(v) / vectorScale));
            int nTangents = sourceMeshClone.tangents.Length;
            Vector4[] tangents = PremultiplyTangents(sourceMeshClone.tangents, vertMatrices, nVertices, vectorScale);

            Debug.Log(sw.ElapsedMilliseconds);

            //uvs[0].AddRange(srcMesh.uv);

            Vector2[] uv0 = sourceMeshClone.uv;

            Vector4[] uv1 = ClampedBoneWeights(boneWeights);

            Debug.Log(sw.ElapsedMilliseconds);

            // uvs[1].AddRange(boneWeights.Select(bw => ClampBoneWeight((object[])bw)));

            sourceMeshClone.SetVertices(vertices);
            sourceMeshClone.SetNormals(normals);
            sourceMeshClone.SetTangents(tangents);
            sourceMeshClone.SetUVs(0, uv0);
            sourceMeshClone.SetUVs(1, uv1);

            Debug.Log(sw.ElapsedMilliseconds);

            currentInstanceIndex = AddSubmeshesAsInstances(
                sourceMeshClone,
                combineInstances,
                currentInstanceIndex);

            Debug.Log(sw.ElapsedMilliseconds);
        }

        resultMesh.CombineMeshes(combineInstances, false);
        resultMesh.RecalculateBounds();
        /*shapeTex.Reinitialize(256, colors.Count / 256, TextureFormat.RGBAFloat, false);
        shapeTex.SetPixels(colors.ToArray());
        shapeTex.Apply(false, false);*/
        //CreateBoneTex(boneTex, bindposes);

        return resultMesh;


    }

    Transform[] GetAnimatorBones(Animator animator)
    {
        int nBones = HumanTrait.BoneCount;
        Transform[] bones = new Transform[nBones];
        for (int i = 0; i < nBones; i++)
        {
            Transform bone = animator.GetBoneTransform((HumanBodyBones)i);
            bones[i] = bone;

            Debug.Log(bone != null ? bone.name : "NULL");
        }
        return bones;
    }

    void FixUnavailableBones(Transform[] bones)
    {
        int nDummyBones = dummyHumanBones.Length;
        for (int index = 0; index < nDummyBones; index += 2)
        {
            int dummyBoneIndex = index + 0;
            int dummyBoneParentIndex = index + 1;

            int boneIndex = dummyHumanBones[dummyBoneIndex];
            int boneParentIndex = dummyHumanBones[dummyBoneParentIndex];

            Transform bone = bones[boneIndex];
            Transform boneParent = bones[boneParentIndex];

            if (bone == null && boneParent != null)
            {
                bones[boneIndex] = boneParent;
            }
        }
    }

    int[] GetActualParentsIndices(Transform[] bones)
    {
        int[] parentIndices = Repeat(-1, bones.Length);
        for (int boneIndex = 0; boneIndex < HumanTrait.BoneCount; boneIndex++) // human bones use human hierarchy
        {
            for (var parentBoneIndex = boneIndex; parentIndices[boneIndex] < 0 && parentBoneIndex != (int)HumanBodyBones.Hips;)
            {
                parentBoneIndex = HumanTrait.GetParentBone(parentBoneIndex);
                bool isParentBoneValid = parentBoneIndex >= 0 && bones[parentBoneIndex];
                parentIndices[boneIndex] = isParentBoneValid ? parentBoneIndex : -1;
            }
        }
        return parentIndices;
    }


    Transform[] PrepareBones(Transform[] humanBones)
    {
        //Transform[] bones = GetAnimatorBones(animator);
        FixUnavailableBones(humanBones);
        //GetActualParentsIndices(humanBones);
        return humanBones;
    }
}
