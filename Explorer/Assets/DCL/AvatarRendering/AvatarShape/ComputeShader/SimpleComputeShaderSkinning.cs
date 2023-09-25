using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Utility;
using Random = UnityEngine.Random;

public class SimpleComputeShaderSkinning
{
    //Shader
    private static int _BaseColour_ShaderID = Shader.PropertyToID("_BaseColor");
    private static readonly int _BaseMapArr_ShaderID = Shader.PropertyToID("_BaseMapArr_ID");
    private static readonly int _Last_Avatar_Vert_Count = Shader.PropertyToID("_lastAvatarVertCount");
    private static readonly int _Last_Wearable_Vert_Count = Shader.PropertyToID("_lastWearableVertCount");


    private static int _AlphaTextureArr_ShaderID = Shader.PropertyToID("_AlphaTextureArr_ID");
    private static int _MetallicGlossMapArr_ShaderID = Shader.PropertyToID("_MetallicGlossMapArr_ID");
    private static int _BumpMapArr_ShaderID = Shader.PropertyToID("_BumpMapArr_ID");
    private static int _EmissionMapArr_ShaderID = Shader.PropertyToID("_EmissionMapArr_ID");

    //Compute shader
    private readonly int BONE_COUNT = 62;
    private int skinnedMeshRendererBoneCount;
    private int kernel;
    private int skinnedMeshRendererCount;
    private int vertCount;

    private ComputeShader cs;
    private ComputeBuffer mBones;


    public void ComputeSkinning(NativeArray<float4x4> bonesResult)
    {
        mBones.SetData(bonesResult);
        cs.Dispatch(kernel, (vertCount / 64) + 1, 1, 1);
    }

    public int Initialize(List<GameObject> gameObjects, Transform[] bones, TextureArrayContainer textureArrayContainer,
        ComputeShader skinningShader, Material avatarMaterial, int lastAvatarVertCount)
    {
        SetupCounters(gameObjects);
        SetupComputeShader(gameObjects, bones, skinningShader, lastAvatarVertCount);
        SetupMeshRenderer(gameObjects, textureArrayContainer, avatarMaterial, lastAvatarVertCount);
        return vertCount;
    }

    private void SetupComputeShader(List<GameObject> gameObjects, Transform[] bones, ComputeShader skinningShader, int lastAvatarVertCount)
    {

        //Setting up pool arrays
        var totalVertsIn = new NativeArray<Vector3>(vertCount, Allocator.Temp);
        var totalNormalsIn = new NativeArray<Vector3>(vertCount, Allocator.Temp);
        var totalSkinIn = new NativeArray<BoneWeight>(vertCount, Allocator.Temp);
        var bindPosesIndexList = new NativeArray<int>(vertCount, Allocator.Temp);
        var bindPosesMatrix = new NativeArray<Matrix4x4>(skinnedMeshRendererBoneCount, Allocator.Temp);

        int vertCounter = 0;
        int skinnedMeshCounter = 0;

        foreach (GameObject gameObject in gameObjects)
        {
            Transform rootTransform = gameObject.transform;

            foreach (SkinnedMeshRenderer skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                int meshVertexCount = skinnedMeshRenderer.sharedMesh.vertexCount;
                ResetTransforms(skinnedMeshRenderer, rootTransform);
                FillMeshArray(skinnedMeshRenderer, bindPosesMatrix, bindPosesIndexList, totalVertsIn, totalNormalsIn, totalSkinIn, meshVertexCount, vertCounter, skinnedMeshCounter);
                vertCounter += meshVertexCount;
                skinnedMeshCounter++;
            }
        }

        SetupBuffers(bones, totalVertsIn, totalNormalsIn, totalSkinIn, bindPosesMatrix, bindPosesIndexList, skinningShader, lastAvatarVertCount);

    }

    private void SetupBuffers(Transform[] bones, NativeArray<Vector3> vertsIn, NativeArray<Vector3> normsIn, NativeArray<BoneWeight> totalSkinIn,
        NativeArray<Matrix4x4> bindPosesMatrix, NativeArray<int> bindPosesIndexList, ComputeShader skinningShader, int lastAvatarVertCount)
    {
        //TODO: Find out why adding ComputeBufferType.Constant doesnt work in Windows, but it does in Mac
        var vertexIn = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(Vector3)));
        vertexIn.SetData(vertsIn);
        var normalsIn = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(Vector3)));
        normalsIn.SetData(normsIn);
        var sourceSkin = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(BoneWeight)));
        sourceSkin.SetData(totalSkinIn);
        var bindPoses = new ComputeBuffer(skinnedMeshRendererBoneCount, Marshal.SizeOf(typeof(Matrix4x4)));
        bindPoses.SetData(bindPosesMatrix);
        var bindPosesIndex = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(int)));
        bindPosesIndex.SetData(bindPosesIndexList);
        mBones = new ComputeBuffer(bones.Length, Marshal.SizeOf(typeof(Matrix4x4)));

        cs = Object.Instantiate(skinningShader);
        kernel = cs.FindKernel("main");
        cs.SetInt(Shader.PropertyToID("g_VertCount"), vertCount);
        cs.SetInt(Shader.PropertyToID("_lastAvatarVertCount"), lastAvatarVertCount);
        cs.SetBuffer(kernel, Shader.PropertyToID("g_VertsIn"), vertexIn);
        cs.SetBuffer(kernel, Shader.PropertyToID("g_NormalsIn"), normalsIn);
        cs.SetBuffer(kernel, Shader.PropertyToID("g_SourceSkin"), sourceSkin);
        cs.SetBuffer(kernel, Shader.PropertyToID("g_BindPoses"), bindPoses);
        cs.SetBuffer(kernel, Shader.PropertyToID("g_BindPosesIndex"), bindPosesIndex);
        cs.SetBuffer(kernel, Shader.PropertyToID("g_mBones"), mBones);
    }

    private void FillMeshArray(SkinnedMeshRenderer skinnedMeshRenderer, NativeArray<Matrix4x4> bindPosesMatrix,
        NativeArray<int> bindPosesIndexList, NativeArray<Vector3> totalVertsIn, NativeArray<Vector3> totalNormalsIn, NativeArray<BoneWeight> totalSkinIn,
        int currentMeshVertexCount, int vertexCounter, int skinnedMeshCounter)
    {
        Mesh mesh = skinnedMeshRenderer.sharedMesh;

        //TODO: Is it possible to remove this allocation?
        NativeArray<Matrix4x4>.Copy(mesh.bindposes, 0, bindPosesMatrix, BONE_COUNT * skinnedMeshCounter, BONE_COUNT);
        NativeArray<BoneWeight>.Copy(mesh.boneWeights, 0, totalSkinIn, vertexCounter, currentMeshVertexCount);
        NativeArray<Vector3>.Copy(mesh.vertices, 0, totalVertsIn, vertexCounter, currentMeshVertexCount);
        NativeArray<Vector3>.Copy(mesh.normals, 0, totalNormalsIn, vertexCounter, currentMeshVertexCount);

        //Setup vertex index for current wearable
        for (var i = 0; i < mesh.vertexCount; i++)
            bindPosesIndexList[vertexCounter + i] = BONE_COUNT * skinnedMeshCounter;
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

        skinnedMeshRendererBoneCount = skinnedMeshRendererCount * BONE_COUNT;
    }

    private void SetupMeshRenderer(List<GameObject> gameObjects, TextureArrayContainer textureArrayContainer, Material avatarMaterial, int lastAvatarVertCount)
    {
        var auxVertCounter = 0;

        foreach (GameObject gameObject in gameObjects)
        {
            foreach (SkinnedMeshRenderer skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                int currentVertexCount = skinnedMeshRenderer.sharedMesh.vertexCount;
                MeshRenderer renderer = SetupMesh(skinnedMeshRenderer);
                SetupMaterial(renderer, auxVertCounter, textureArrayContainer, avatarMaterial, lastAvatarVertCount);
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

    private void SetupMaterial(MeshRenderer meshRenderer, int lastWearableVertCount, TextureArrayContainer m_TextureArrays, Material celShadingMaterial, int lastAvatarVertCount)
    {
        var albedoTexture = (Texture2D)meshRenderer.material.mainTexture;
        var vertOutMaterial = new Material(celShadingMaterial);

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

        //meshRenderer.realtimeLightmapIndex = 0; // turn on real-time lightmap
        //meshRenderer.realtimeLightmapScaleOffset = new UnityEngine.Vector4(lastWearableVertCount, lastAvatarVertCount, 0,0);

        vertOutMaterial.SetInteger(_Last_Wearable_Vert_Count, lastWearableVertCount);
        vertOutMaterial.SetInteger(_Last_Avatar_Vert_Count, lastAvatarVertCount);
        vertOutMaterial.SetColor("_BaseColor", Random.ColorHSV());
        meshRenderer.material = vertOutMaterial;

    }
}
