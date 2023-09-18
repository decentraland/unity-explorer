using DCL.AvatarRendering.AvatarShape.ComputeShader;
using Stella3D;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

public class SimpleComputeShaderSkinning
{
    public ComputeShader cs;
    private ComputeBuffer sourceVBO;
    private ComputeBuffer sourceSkin;
    private ComputeBuffer meshVertsOut;
    private ComputeBuffer mBones;

    private Matrix4x4[] boneMatrices;
    private Matrix4x4[] bindPoses;
    public Transform[] bones;

    private int vertCount;

    public BoneMatrixCalculationJob BoneMatrixCalculation;
    private GameObject go;
    private MeshFilter filter;

    private struct SVertInVBO
    {
        public Vector3 pos;
        public Vector3 norm;
        public Vector4 tang;
    }

    private struct SVertOut
    {
        public Vector3 pos;
        private Vector3 norm;
        private Vector4 tang;
    }

    private struct SVertInSkin
    {
        public float weight0, weight1, weight2, weight3;
        public int index0, index1, index2, index3;
    }

    private SVertOut[] vertOutArray;
    private Vector3[] verticesForMesh;

    public void ComputeSkinning()
    {
        int kernel = cs.FindKernel("main");
        cs.SetBuffer(kernel, "g_SourceVBO", sourceVBO);
        cs.SetBuffer(kernel, "g_SourceSkin", sourceSkin);
        cs.SetBuffer(kernel, "g_MeshVertsOut", meshVertsOut);
        cs.SetBuffer(kernel, "g_mBones", mBones);
        cs.SetInt("g_VertCount", vertCount);
        cs.Dispatch(kernel, (vertCount / 64) + 1, 1, 1);

        //meshVertsOut.GetData(vertOutArray);
        //for (var index = 0; index < vertOutArray.Length; index++)
        //    verticesForMesh[index] = vertOutArray[index].pos;
        //filter.mesh.vertices = verticesForMesh;
    }

    public void Initialize(SkinnedMeshRenderer skin, Transform[] bones, Transform avatarBaseTransform)
    {
        cs = Resources.Load<ComputeShader>("Skinning");
        Mesh mesh = skin.sharedMesh;
        this.bones = bones;
        bindPoses = mesh.bindposes;
        vertCount = mesh.vertexCount;

        SVertInVBO[] inVBO = Enumerable.Range(0, vertCount)
                                       .Select(
                                            idx => new SVertInVBO
                                            {
                                                pos = mesh.vertices[idx],
                                                norm = mesh.normals[idx],
                                            })
                                       .ToArray();

        sourceVBO = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(SVertInVBO)));
        sourceVBO.SetData(inVBO);

        SVertInSkin[] inSkin = mesh.boneWeights.Select(
                                        w => new SVertInSkin
                                        {
                                            weight0 = w.weight0,
                                            weight1 = w.weight1,
                                            weight2 = w.weight2,
                                            weight3 = w.weight3,
                                            index0 = w.boneIndex0,
                                            index1 = w.boneIndex1,
                                            index2 = w.boneIndex2,
                                            index3 = w.boneIndex3,
                                        })
                                   .ToArray();

        sourceSkin = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(SVertInSkin)));
        sourceSkin.SetData(inSkin);
        meshVertsOut = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(SVertOut)));
        mBones = new ComputeBuffer(bones.Length, Marshal.SizeOf(typeof(Matrix4x4)));
        boneMatrices = bones.Select((b, idx) => skin.gameObject.transform.worldToLocalMatrix * b.localToWorldMatrix * mesh.bindposes[idx]).ToArray();

        this.avatarBaseTransform = avatarBaseTransform;
        SetupBurstJob();

        //vertOutArray = new SVertOut[vertCount];
        //verticesForMesh = new Vector3[vertCount];

        //Creating mesh renderer and setting propierties
        go = skin.gameObject;
        filter = go.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
        filter.mesh = skin.sharedMesh;
        meshRenderer.material = skin.material;

        var vertOutMaterial = new Material(Resources.Load<Material>("VertOutMaterial"));
        vertOutMaterial.mainTexture = meshRenderer.material.mainTexture;

        var mpb = new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(mpb);
        mpb.SetBuffer("_VertIn", meshVertsOut);
        meshRenderer.SetPropertyBlock(mpb);
        meshRenderer.material = vertOutMaterial;

        Object.Destroy(skin);
    }

    public TransformAccessArray Bones;
    public NativeArray<float4x4> boneSharedArray;
    public Transform avatarBaseTransform;
    public BoneMatrixCalculationJob job;

    private void SetupBurstJob()
    {
        Bones = new TransformAccessArray(bones);
        boneSharedArray = new NativeArray<float4x4>(bones.Length, Allocator.Persistent);

        /*NativeArray<float4x4> bindPosesNA = new NativeArray<float4x4>(bindPoses.Length, Allocator.Persistent);
        // Copy data from the source array (Matrix4x4) to the destination NativeArray (float4x4)
        for (int i = 0; i < bindPoses.Length; i++)
        {
            Matrix4x4 sourceMatrix = bindPoses[i];
            float4x4 destinationMatrix = new float4x4(
                new float4(sourceMatrix.m00, sourceMatrix.m01, sourceMatrix.m02, sourceMatrix.m03),
                new float4(sourceMatrix.m10, sourceMatrix.m11, sourceMatrix.m12, sourceMatrix.m13),
                new float4(sourceMatrix.m20, sourceMatrix.m21, sourceMatrix.m22, sourceMatrix.m23),
                new float4(sourceMatrix.m30, sourceMatrix.m31, sourceMatrix.m32, sourceMatrix.m33)
            );
            bindPosesNA[i] = destinationMatrix;
        }*/
    }

    public void DoBoneMatrixCalculation()
    {
        //for (var i = 0; i < boneMatrices.Length; i++)
        //    boneMatrices[i] = go.transform.worldToLocalMatrix * bones[i].localToWorldMatrix * bindPoses[i];
        //mBones.SetData(boneMatrices);

        job = new BoneMatrixCalculationJob
        {
            BonesMatricesResult = boneSharedArray,
            BindPoses = new SharedArray<Matrix4x4, float4x4>(bindPoses),
            AvatarTransform = avatarBaseTransform.worldToLocalMatrix,
        };

        JobHandle handle = job.Schedule(Bones);
        handle.Complete();
        mBones.SetData(boneSharedArray);
    }
}
