using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Utility;

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

    private int vertCount;
    private int kernel;


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


    public void ComputeSkinning(NativeArray<float4x4> bonesResult)
    {
        mBones.SetData(bonesResult);
        cs.Dispatch(kernel, (vertCount / 64) + 1, 1, 1);
    }

    public void Initialize(List<GameObject> gameObjects, Transform[] bones)
    {
        SetupComputeShader(gameObjects, bones);
        SetupMeshRenderer(gameObjects);
    }

    private void SetupComputeShader(List<GameObject> gameObjects, Transform[] bones)
    {
        var totalVertsIn = new List<SVertInVBO>();
        var totalSkinIn = new List<SVertInSkin>();
        var bindPosesMatrix = new List<Matrix4x4>();
        var bindPosesIndexList = new List<int>();
        var amountOfSkinnedMeshRenderer = 0;

        foreach (GameObject gameObject in gameObjects)
        {
            Transform rootTransform = gameObject.transform;

            foreach (SkinnedMeshRenderer skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                // Make sure that Transform is uniform with the root
                // Non-uniform does not make sense as skin relatively to the base avatar
                // so we just waste calculations on transformation matrices
                Transform currentTransform = skinnedMeshRenderer.transform;

                while (currentTransform != rootTransform)
                {
                    currentTransform.ResetLocalTRS();
                    currentTransform = currentTransform.parent;
                }


                Mesh mesh = skinnedMeshRenderer.sharedMesh;
                bindPosesMatrix.AddRange(mesh.bindposes);
                int currentVertexCount = mesh.vertexCount;

                //Setup vertex index for current wearable
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
        mBones.SetData(new Matrix4x4[bones.Length]);

        cs = Object.Instantiate(Resources.Load<ComputeShader>("Skinning"));
        kernel = cs.FindKernel("main");
        cs.SetInt(Shader.PropertyToID("g_VertCount"), vertCount);
        cs.SetBuffer(kernel, Shader.PropertyToID("g_SourceVBO"), sourceVBO);
        cs.SetBuffer(kernel, Shader.PropertyToID("g_SourceSkin"), sourceSkin);
        cs.SetBuffer(kernel, Shader.PropertyToID("g_BindPoses"), bindPoses);
        cs.SetBuffer(kernel, Shader.PropertyToID("g_BindPosesIndex"), bindPosesIndex);
        cs.SetBuffer(kernel, Shader.PropertyToID("g_mBones"), mBones);
        cs.SetBuffer(kernel, Shader.PropertyToID("g_MeshVertsOut"), meshVertsOut);
    }

    private void SetupMeshRenderer(List<GameObject> gameObjects)
    {
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


}
