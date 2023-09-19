using System.Linq;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class SimpleComputeShaderSkinning
{
    private SVertOut[] vertOutArray;
    private Vector3[] verticesForMesh;

    public ComputeShader cs;
    private ComputeBuffer sourceVBO;
    private ComputeBuffer sourceSkin;
    private ComputeBuffer meshVertsOut;
    private ComputeBuffer mBones;

    private Matrix4x4[] boneMatrices;

    private int vertCount;

    private GameObject go;
    private MeshFilter filter;
    private MeshRenderer meshRenderer;




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

    private int kernel;

    public void ComputeSkinning(NativeArray<float4x4> bonesResult)
    {
        mBones.SetData(bonesResult);
        cs.GetKernelThreadGroupSizes(kernel, out uint x, out uint y, out uint z);
        cs.Dispatch(kernel, (int)x, (int)y, (int)z);

        //meshVertsOut.GetData(vertOutArray);
        //for (var index = 0; index < vertOutArray.Length; index++)
        //    verticesForMesh[index] = vertOutArray[index].pos;
        //filter.mesh.vertices = verticesForMesh;
    }

    public void Initialize(SkinnedMeshRenderer skin, Transform[] bones, Transform avatarBaseTransform)
    {
        SetupComputeShader(skin, bones);
        SetupMesh(skin);
        SetupMaterial();

        //SetupBurstJob(avatarBaseTransform, bones);


        //vertOutArray = new SVertOut[vertCount];
        //verticesForMesh = new Vector3[vertCount];


    }

    private void SetupMesh(SkinnedMeshRenderer skin)
    {
        //Creating mesh renderer and setting propierties
        go = skin.gameObject;
        filter = go.AddComponent<MeshFilter>();
        meshRenderer = go.AddComponent<MeshRenderer>();
        filter.mesh = skin.sharedMesh;
        meshRenderer.material = skin.material;
        Object.Destroy(skin);
    }

    private void SetupMaterial()
    {
        var vertOutMaterial = new Material(Resources.Load<Material>("VertOutMaterial"));
        vertOutMaterial.mainTexture = meshRenderer.material.mainTexture;
        var mpb = new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(mpb);
        mpb.SetBuffer("_VertIn", meshVertsOut);
        meshRenderer.SetPropertyBlock(mpb);
        meshRenderer.material = vertOutMaterial;
    }

    private void SetupComputeShader(SkinnedMeshRenderer skin, Transform[] bones)
    {
        cs = Object.Instantiate(Resources.Load<ComputeShader>("Skinning"));
        Mesh mesh = skin.sharedMesh;
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
        boneMatrices = new Matrix4x4[bones.Length];
        mBones.SetData(boneMatrices);

        kernel = cs.FindKernel("main");
        cs.SetInt(Shader.PropertyToID("g_VertCount"), vertCount);
        cs.SetBuffer(kernel, Shader.PropertyToID("g_SourceVBO"), sourceVBO);
        cs.SetBuffer(kernel, Shader.PropertyToID("g_SourceSkin"), sourceSkin);
        cs.SetBuffer(kernel, Shader.PropertyToID("g_mBones"), mBones);
        cs.SetBuffer(kernel, Shader.PropertyToID("g_MeshVertsOut"), meshVertsOut);
    }


}
