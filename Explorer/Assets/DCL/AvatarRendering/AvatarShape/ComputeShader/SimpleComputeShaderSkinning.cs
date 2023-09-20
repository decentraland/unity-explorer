using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;

public class SimpleComputeShaderSkinning
{
    private SVertOut[] vertOutArray;
    private Vector3[] verticesForMesh;

    private ComputeShader cs;
    private ComputeBuffer sourceVBO;
    private ComputeBuffer sourceSkin;
    private ComputeBuffer meshVertsOut;
    private ComputeBuffer mBones;
    private ComputeBuffer bindPoses;
    private ComputeBuffer bindPosesIndex;

    private int vertCount;
    private int skinnedMeshRendererCount;
    private int boneCount;
    private int kernel;

    private readonly int BONE_COUNT = 62;


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
        if (m_TextureArrays == null)
            m_TextureArrays = new TextureArrayContainer();

        SetupComputeShader(gameObjects, bones);
        SetupMeshRenderer(gameObjects);
    }

    private void SetupComputeShader(List<GameObject> gameObjects, Transform[] bones)
    {
        SetupCounters(gameObjects);

        //Setting up pool arrays
        SVertInVBO[] totalVertsIn = ArrayPool<SVertInVBO>.Shared.Rent(vertCount);
        SVertInSkin[] totalSkinIn = ArrayPool<SVertInSkin>.Shared.Rent(vertCount);
        int[] bindPosesIndexList = ArrayPool<int>.Shared.Rent(vertCount);
        Matrix4x4[] bindPosesMatrix = ArrayPool<Matrix4x4>.Shared.Rent(boneCount);

        //Resetting vert counters for filling
        vertCount = 0;
        skinnedMeshRendererCount = 0;

        foreach (GameObject gameObject in gameObjects)
        {
            Transform rootTransform = gameObject.transform;
            foreach (SkinnedMeshRenderer skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                ResetTransforms(skinnedMeshRenderer, rootTransform);
                FillMeshArray(skinnedMeshRenderer, bindPosesMatrix, bindPosesIndexList, totalVertsIn, totalSkinIn);
                vertCount += skinnedMeshRenderer.sharedMesh.vertexCount;
                skinnedMeshRendererCount++;
            }
        }

        SetupBuffers(bones, totalVertsIn, totalSkinIn, bindPosesMatrix, bindPosesIndexList);

        ArrayPool<SVertInVBO>.Shared.Return(totalVertsIn);
        ArrayPool<SVertInSkin>.Shared.Return(totalSkinIn);
        ArrayPool<int>.Shared.Return(bindPosesIndexList);
        ArrayPool<Matrix4x4>.Shared.Return(bindPosesMatrix);
    }

    private void SetupBuffers(Transform[] bones, SVertInVBO[] totalVertsIn, SVertInSkin[] totalSkinIn,
        Matrix4x4[] bindPosesMatrix, int[] bindPosesIndexList)
    {
        sourceVBO = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(SVertInVBO)));
        sourceVBO.SetData(totalVertsIn[..vertCount]);
        sourceSkin = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(SVertInSkin)));
        sourceSkin.SetData(totalSkinIn[..vertCount]);
        bindPoses = new ComputeBuffer(boneCount, Marshal.SizeOf(typeof(Matrix4x4)));
        bindPoses.SetData(bindPosesMatrix[..boneCount]);
        bindPosesIndex = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(int)));
        bindPosesIndex.SetData(bindPosesIndexList[..vertCount]);
        meshVertsOut = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(SVertOut)));
        mBones = new ComputeBuffer(bones.Length, Marshal.SizeOf(typeof(Matrix4x4)));

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

    private void FillMeshArray(SkinnedMeshRenderer skinnedMeshRenderer, Matrix4x4[] bindPosesMatrix,
        int[] bindPosesIndexList, SVertInVBO[] totalVertsIn, SVertInSkin[] totalSkinIn)
    {
        Mesh mesh = skinnedMeshRenderer.sharedMesh;
        Array.Copy(mesh.bindposes, 0, bindPosesMatrix, BONE_COUNT * skinnedMeshRendererCount, mesh.bindposes.Length);

        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector4[] tangent = mesh.tangents;
        BoneWeight[] boneWeight = mesh.boneWeights;

        //Setup vertex index for current wearable
        for (var i = 0; i < mesh.vertexCount; i++)
        {
            bindPosesIndexList[vertCount + i] = BONE_COUNT * skinnedMeshRendererCount;

            totalVertsIn[vertCount + i] = new SVertInVBO
            {
                pos = vertices[i],
                norm = normals[i],

                //tang = tangent[i]
            };

            totalSkinIn[vertCount + i] = new SVertInSkin
            {
                weight0 = boneWeight[i].weight0,
                weight1 = boneWeight[i].weight1,
                weight2 = boneWeight[i].weight2,
                weight3 = boneWeight[i].weight3,
                index0 = boneWeight[i].boneIndex0,
                index1 = boneWeight[i].boneIndex1,
                index2 = boneWeight[i].boneIndex2,
                index3 = boneWeight[i].boneIndex3,
            };
        }
    }

    private static void ResetTransforms(SkinnedMeshRenderer skinnedMeshRenderer, Transform rootTransform)
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
    }

    private void SetupCounters(List<GameObject> gameObjects)
    {
        foreach (GameObject gameObject in gameObjects)
        foreach (SkinnedMeshRenderer skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            vertCount += skinnedMeshRenderer.sharedMesh.vertexCount;
            skinnedMeshRendererCount++;
        }

        boneCount = skinnedMeshRendererCount * BONE_COUNT;
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
        var albedoTexture = (Texture2D)meshRenderer.material.mainTexture;
        var vertOutMaterial = new Material(Resources.Load<Material>("Avatar_CelShading"));

        if (albedoTexture != null)
        {
            if (albedoTexture.width.Equals(512))
            {
                Graphics.CopyTexture(albedoTexture, srcElement: 0, srcMip: 0, m_TextureArrays.texture2DArray_512_BaseMap, dstElement: m_TextureArrays.textureArrayCount_512_BaseMap, dstMip: 0);
                vertOutMaterial.SetInteger(_BaseMapArr_ShaderID, m_TextureArrays.textureArrayCount_512_BaseMap);
                vertOutMaterial.SetTexture("_BaseMapArr", m_TextureArrays.texture2DArray_512_BaseMap);
                m_TextureArrays.textureArrayCount_512_BaseMap++;
            }
            else
            {
                Graphics.CopyTexture(albedoTexture, srcElement: 0, srcMip: 0, m_TextureArrays.texture2DArray_256_BaseMap, dstElement: m_TextureArrays.textureArrayCount_256_BaseMap, dstMip: 0);
                vertOutMaterial.SetInteger(_BaseMapArr_ShaderID, m_TextureArrays.textureArrayCount_256_BaseMap);
                vertOutMaterial.SetTexture("_BaseMapArr", m_TextureArrays.texture2DArray_256_BaseMap);
                m_TextureArrays.textureArrayCount_256_BaseMap++;
            }
        }

        vertOutMaterial.SetInt("_startIndex", startIndex);
        vertOutMaterial.SetBuffer("_VertIn", meshVertsOut);

        meshRenderer.material = vertOutMaterial;

        /*var mpb = new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(mpb);
        mpb.SetBuffer("_VertIn", meshVertsOut);
        mpb.SetInt("_startIndex", startIndex);
        meshRenderer.SetPropertyBlock(mpb);*/
    }

    private static TextureArrayContainer m_TextureArrays;
    private static int _BaseColour_ShaderID = Shader.PropertyToID("_BaseColor");
    private static readonly int _BaseMapArr_ShaderID = Shader.PropertyToID("_BaseMapArr_ID");
    private static int _AlphaTextureArr_ShaderID = Shader.PropertyToID("_AlphaTextureArr_ID");
    private static int _MetallicGlossMapArr_ShaderID = Shader.PropertyToID("_MetallicGlossMapArr_ID");
    private static int _BumpMapArr_ShaderID = Shader.PropertyToID("_BumpMapArr_ID");
    private static int _EmissionMapArr_ShaderID = Shader.PropertyToID("_EmissionMapArr_ID");



}
