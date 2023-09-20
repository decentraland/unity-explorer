using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Utility;

public class SimpleComputeShaderSkinning
{
    private readonly int BONE_COUNT = 62;
    private int boneCount;
    private ComputeShader cs;
    private int kernel;
    private ComputeBuffer mBones;
    private ComputeBuffer meshVertsOut;
    private int skinnedMeshRendererCount;

    private int vertCount;

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
        SetupCounters(gameObjects);

        //Setting up pool arrays
        var totalVertsIn = new NativeArray<Vector3>(vertCount, Allocator.Temp);
        var totalSkinIn = new NativeArray<BoneWeight>(vertCount, Allocator.Temp);
        var bindPosesIndexList = new NativeArray<int>(vertCount, Allocator.Temp);
        var bindPosesMatrix = new NativeArray<Matrix4x4>(boneCount, Allocator.Temp);

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

        //ArrayPool<SVertInVBO>.Shared.Return(totalVertsIn);
        //ArrayPool<SVertInSkin>.Shared.Return(totalSkinIn);
        //ArrayPool<int>.Shared.Return(bindPosesIndexList);
        //ArrayPool<Matrix4x4>.Shared.Return(bindPosesMatrix);
    }

    private void SetupBuffers(Transform[] bones, NativeArray<Vector3> totalVertsIn, NativeArray<BoneWeight> totalSkinIn,
        NativeArray<Matrix4x4> bindPosesMatrix, NativeArray<int> bindPosesIndexList)
    {
        var sourceVBO = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(Vector3)), ComputeBufferType.Constant);
        sourceVBO.SetData(totalVertsIn);
        var sourceSkin = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(BoneWeight)), ComputeBufferType.Constant);
        sourceSkin.SetData(totalSkinIn);
        var bindPoses = new ComputeBuffer(boneCount, Marshal.SizeOf(typeof(Matrix4x4)), ComputeBufferType.Constant);
        bindPoses.SetData(bindPosesMatrix);
        var bindPosesIndex = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(int)), ComputeBufferType.Constant);
        bindPosesIndex.SetData(bindPosesIndexList);
        meshVertsOut = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(Vector3)));
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

    private void FillMeshArray(SkinnedMeshRenderer skinnedMeshRenderer, NativeArray<Matrix4x4> bindPosesMatrix,
        NativeArray<int> bindPosesIndexList, NativeArray<Vector3> totalVertsIn, NativeArray<BoneWeight> totalSkinIn)
    {
        Mesh mesh = skinnedMeshRenderer.sharedMesh;

        //TODO: Is it possible to remove this allocation?
        NativeArray<Matrix4x4>.Copy(mesh.bindposes, 0, bindPosesMatrix, BONE_COUNT * skinnedMeshRendererCount, BONE_COUNT);
        NativeArray<BoneWeight>.Copy(mesh.boneWeights, 0, totalSkinIn, vertCount, mesh.vertexCount);
        NativeArray<Vector3>.Copy(mesh.vertices, 0, totalVertsIn, vertCount, mesh.vertexCount);

        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;

        //Setup vertex index for current wearable
        for (var i = 0; i < mesh.vertexCount; i++)
            bindPosesIndexList[vertCount + i] = BONE_COUNT * skinnedMeshRendererCount;
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
