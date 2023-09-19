using System.Collections.Generic;
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
    private ComputeBuffer bindPoses;
    private ComputeBuffer bindPosesIndex;


    private Matrix4x4[] boneMatrices;

    private int vertCount;

    //private GameObject go;
    //private MeshFilter filter;
    //private MeshRenderer meshRenderer;



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
        cs.Dispatch(kernel, (vertCount / 64) + 1, 1, 1);

        //meshVertsOut.GetData(vertOutArray);
        //for (var index = 0; index < vertOutArray.Length; index++)
        //    verticesForMesh[index] = vertOutArray[index].pos;
        //filter.mesh.vertices = verticesForMesh;
    }

    public void Initialize(List<GameObject> gameObjects, Transform[] bones)
    {
        var totalVertsIn = new List<SVertInVBO>();
        var totalSkinIn = new List<SVertInSkin>();
        var bindPosesMatrix = new List<Matrix4x4>();
        var bindPosesIndexList = new List<int>();
        var amountOfSkinnedMeshRenderer = 0;

        foreach (GameObject gameObject in gameObjects)
        {
            foreach (SkinnedMeshRenderer skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                Mesh mesh = skinnedMeshRenderer.sharedMesh;
                bindPosesMatrix.AddRange(mesh.bindposes);
                int currentVertexCount = mesh.vertexCount;

                for (var i = 0; i < currentVertexCount; i++)
                {
                    bindPosesIndexList.Add(62 * amountOfSkinnedMeshRenderer);

                    totalVertsIn.Add(new SVertInVBO
                    {
                        pos = mesh.vertices[i],
                        norm = mesh.normals[i],
                    });

                    totalSkinIn.Add(new SVertInSkin
                    {
                        weight0 = mesh.boneWeights[i].weight0,
                        weight1 = mesh.boneWeights[i].weight1,
                        weight2 = mesh.boneWeights[i].weight2,
                        weight3 = mesh.boneWeights[i].weight3,
                        index0 = mesh.boneWeights[i].boneIndex0,
                        index1 = mesh.boneWeights[i].boneIndex1,
                        index2 = mesh.boneWeights[i].boneIndex2,
                        index3 = mesh.boneWeights[i].boneIndex3,
                    });
                }

                vertCount += skinnedMeshRenderer.sharedMesh.vertexCount;
                amountOfSkinnedMeshRenderer++;
            }
        }

        sourceVBO = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(SVertInVBO)));
        sourceVBO.SetData(totalVertsIn.ToArray());
        sourceSkin = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(SVertInSkin)));
        sourceSkin.SetData(totalSkinIn.ToArray());
        bindPoses = new ComputeBuffer(bindPosesMatrix.Count, Marshal.SizeOf(typeof(Matrix4x4)));
        bindPoses.SetData(bindPosesMatrix.ToArray());
        bindPosesIndex = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(int)));
        bindPosesIndex.SetData(bindPosesIndexList.ToArray());

        meshVertsOut = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(SVertOut)));
        mBones = new ComputeBuffer(bones.Length, Marshal.SizeOf(typeof(Matrix4x4)));
        boneMatrices = new Matrix4x4[bones.Length];
        mBones.SetData(boneMatrices);

        cs = Object.Instantiate(Resources.Load<ComputeShader>("Skinning"));
        kernel = cs.FindKernel("main");
        cs.SetInt(Shader.PropertyToID("g_VertCount"), vertCount);
        cs.SetBuffer(kernel, Shader.PropertyToID("g_SourceVBO"), sourceVBO);
        cs.SetBuffer(kernel, Shader.PropertyToID("g_SourceSkin"), sourceSkin);
        cs.SetBuffer(kernel, Shader.PropertyToID("g_BindPoses"), bindPoses);
        cs.SetBuffer(kernel, Shader.PropertyToID("g_BindPosesIndex"), bindPosesIndex);
        cs.SetBuffer(kernel, Shader.PropertyToID("g_mBones"), mBones);
        cs.SetBuffer(kernel, Shader.PropertyToID("g_MeshVertsOut"), meshVertsOut);

        var auxVertCounter = 0;

        foreach (GameObject gameObject in gameObjects)
        {
            foreach (SkinnedMeshRenderer skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                int currentVertexCount = skinnedMeshRenderer.sharedMesh.vertexCount;
                MeshRenderer renderer = SetupMesh(skinnedMeshRenderer);
                SetupMaterial(renderer, auxVertCounter);
                auxVertCounter += currentVertexCount;
            }
        }


    }

    private MeshRenderer SetupMesh(SkinnedMeshRenderer skin)
    {
        //Creating mesh renderer and setting propierties
        GameObject go = skin.gameObject;
        MeshFilter filter = go.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
        filter.mesh = skin.sharedMesh;
        meshRenderer.material = skin.material;
        Object.Destroy(skin);
        return meshRenderer;
    }

    private void SetupMaterial(MeshRenderer meshRenderer, int startIndex)
    {
        var vertOutMaterial = new Material(Resources.Load<Material>("VertOutMaterial"));
        vertOutMaterial.mainTexture = meshRenderer.material.mainTexture;
        var mpb = new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(mpb);
        mpb.SetBuffer("_VertIn", meshVertsOut);
        mpb.SetInt("_startIndex", startIndex);
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
