using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Rendering.Avatar;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    public class ComputeShaderSkinning : CustomSkinning
    {
        private int skinnedMeshRendererBoneCount;
        private int kernel;
        private int skinnedMeshRendererCount;
        private int vertCount;

        private UnityEngine.ComputeShader cs;
        private ComputeBuffer mBones;

        private readonly List<UsedTextureArraySlot> usedTextureArraySlots;

        //TODO: Find out why adding ComputeBufferType.Constant doesnt work in Windows, but it does in Mac
        private ComputeBuffer vertexIn;
        private ComputeBuffer tangentsIn;
        private ComputeBuffer normalsIn;
        private ComputeBuffer sourceSkin;
        private ComputeBuffer bindPoses;
        private ComputeBuffer bindPosesIndex;

        public ComputeShaderSkinning()
        {
            usedTextureArraySlots = new List<UsedTextureArraySlot>();
        }

        public override void ComputeSkinning(NativeArray<float4x4> bonesResult)
        {
            NativeArray<float4x4> bonesIn = mBones.BeginWrite<float4x4>(0, ComputeShaderConstants.BONE_COUNT);
            NativeArray<float4x4>.Copy(bonesResult, 0, bonesIn, 0, ComputeShaderConstants.BONE_COUNT);
            mBones.EndWrite<float4x4>(ComputeShaderConstants.BONE_COUNT);
            cs.Dispatch(kernel, (vertCount / 64) + 1, 1, 1);

            //Note (Juani): SetData was slightly faster for me in my Windows Computer using DX11. However, Unity recommends using BeginWrite/EndWrite,
            //so I ll use that one. If there is compalints of different Graphics API, this would be a first to check
            //https://docs.unity3d.com/2020.1/Documentation/ScriptReference/ComputeBuffer.BeginWrite.html
            //mBones.SetData(bonesResult);
        }

        public override int Initialize(List<GameObject> gameObjects, TextureArrayContainer textureArrayContainer,
            UnityEngine.ComputeShader skinningShader, IObjectPool<Material> avatarMaterialPool, int lastAvatarVertCount, SkinnedMeshRenderer baseAvatarSkinnedMeshRenderer, AvatarShapeComponent avatarShapeComponent)
        {
            SetupCounters(gameObjects);
            SetupComputeShader(gameObjects, skinningShader, lastAvatarVertCount);
            SetupMeshRenderer(gameObjects, textureArrayContainer, avatarMaterialPool, lastAvatarVertCount, avatarShapeComponent);
            return vertCount;
        }

        //TODO: Pool this buffer
        private ComputeBuffer CreateSubUpdateBuffer<T>(int size) where T: struct =>
            new (size, Marshal.SizeOf(typeof(T)), ComputeBufferType.Structured, ComputeBufferMode.SubUpdates);

        private void SetupComputeShader(List<GameObject> gameObjects, UnityEngine.ComputeShader skinningShader, int lastAvatarVertCount)
        {
            vertexIn = CreateSubUpdateBuffer<Vector3>(vertCount);
            NativeArray<Vector3> totalVertsIn = vertexIn.BeginWrite<Vector3>(0, vertCount);
            normalsIn = CreateSubUpdateBuffer<Vector3>(vertCount);
            NativeArray<Vector3> totalNormalsIn = normalsIn.BeginWrite<Vector3>(0, vertCount);
            tangentsIn = CreateSubUpdateBuffer<Vector4>(vertCount);
            NativeArray<Vector4> totalTangentsIn = tangentsIn.BeginWrite<Vector4>(0, vertCount);
            sourceSkin = CreateSubUpdateBuffer<BoneWeight>(vertCount);
            NativeArray<BoneWeight> totalSkinIn = sourceSkin.BeginWrite<BoneWeight>(0, vertCount);
            bindPosesIndex = CreateSubUpdateBuffer<int>(vertCount);
            NativeArray<int> bindPosesIndexList = bindPosesIndex.BeginWrite<int>(0, vertCount);
            bindPoses = CreateSubUpdateBuffer<Matrix4x4>(skinnedMeshRendererBoneCount);
            NativeArray<Matrix4x4> bindPosesMatrix = bindPoses.BeginWrite<Matrix4x4>(0, skinnedMeshRendererBoneCount);

            var vertCounter = 0;
            var skinnedMeshCounter = 0;

            foreach (GameObject gameObject in gameObjects)
            {
                Transform rootTransform = gameObject.transform;

                foreach (SkinnedMeshRenderer skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    int meshVertexCount = skinnedMeshRenderer.sharedMesh.vertexCount;
                    ResetTransforms(skinnedMeshRenderer, rootTransform);
                    FillMeshArray(skinnedMeshRenderer, bindPosesMatrix, bindPosesIndexList, totalVertsIn, totalNormalsIn, totalTangentsIn, totalSkinIn, meshVertexCount, vertCounter, skinnedMeshCounter);
                    vertCounter += meshVertexCount;
                    skinnedMeshCounter++;
                }
            }

            SetupBuffers(skinningShader, lastAvatarVertCount);
        }

        private void SetupBuffers(UnityEngine.ComputeShader skinningShader, int lastAvatarVertCount)
        {
            //TODO: Find out why adding ComputeBufferType.Constant doesnt work in Windows, but it does in Mac
            vertexIn.EndWrite<Vector3>(vertCount);
            normalsIn.EndWrite<Vector3>(vertCount);
            tangentsIn.EndWrite<Vector4>(vertCount);
            sourceSkin.EndWrite<BoneWeight>(vertCount);
            bindPosesIndex.EndWrite<int>(vertCount);
            bindPoses.EndWrite<Matrix4x4>(skinnedMeshRendererBoneCount);
            mBones = new ComputeBuffer(ComputeShaderConstants.BONE_COUNT, Marshal.SizeOf(typeof(float4x4)), ComputeBufferType.Structured, ComputeBufferMode.SubUpdates);

            cs = Object.Instantiate(skinningShader);
            kernel = cs.FindKernel(ComputeShaderConstants.SKINNING_KERNEL_NAME);
            cs.SetInt(ComputeShaderConstants.VERT_COUNT_ID, vertCount);
            cs.SetInt(ComputeShaderConstants.LAST_AVATAR_VERT_COUNT_ID, lastAvatarVertCount);
            cs.SetBuffer(kernel, ComputeShaderConstants.VERTS_IN_ID, vertexIn);
            cs.SetBuffer(kernel, ComputeShaderConstants.NORMALS_IN_ID, normalsIn);
            cs.SetBuffer(kernel, ComputeShaderConstants.TANGENTS_IN_ID, tangentsIn);
            cs.SetBuffer(kernel, ComputeShaderConstants.SOURCE_SKIN_ID, sourceSkin);
            cs.SetBuffer(kernel, ComputeShaderConstants.BIND_POSE_ID, bindPoses);
            cs.SetBuffer(kernel, ComputeShaderConstants.BIND_POSES_INDEX_ID, bindPosesIndex);
            cs.SetBuffer(kernel, ComputeShaderConstants.BONES_ID, mBones);
        }

        private void FillMeshArray(SkinnedMeshRenderer skinnedMeshRenderer, NativeArray<Matrix4x4> bindPosesMatrix,
            NativeArray<int> bindPosesIndexList, NativeArray<Vector3> totalVertsIn, NativeArray<Vector3> totalNormalsIn, NativeArray<Vector4> totalTangentsIn, NativeArray<BoneWeight> totalSkinIn,
            int currentMeshVertexCount, int vertexCounter, int skinnedMeshCounter)
        {
            Mesh mesh = skinnedMeshRenderer.sharedMesh;

            // HACK: We only need to do this if the avatar has _NORMALMAPS enabled on the material.
            mesh.RecalculateTangents();

            NativeArray<Matrix4x4>.Copy(mesh.bindposes, 0, bindPosesMatrix, ComputeShaderConstants.BONE_COUNT * skinnedMeshCounter, ComputeShaderConstants.BONE_COUNT);
            NativeArray<BoneWeight>.Copy(mesh.boneWeights, 0, totalSkinIn, vertexCounter, currentMeshVertexCount);
            NativeArray<Vector3>.Copy(mesh.vertices, 0, totalVertsIn, vertexCounter, currentMeshVertexCount);
            NativeArray<Vector3>.Copy(mesh.normals, 0, totalNormalsIn, vertexCounter, currentMeshVertexCount);
            NativeArray<Vector4>.Copy(mesh.tangents, 0, totalTangentsIn, vertexCounter, currentMeshVertexCount);

            //Setup vertex index for current wearable
            for (var i = 0; i < mesh.vertexCount; i++)
                bindPosesIndexList[vertexCounter + i] = ComputeShaderConstants.BONE_COUNT * skinnedMeshCounter;
        }

        private void SetupCounters(List<GameObject> gameObjects)
        {
            foreach (GameObject gameObject in gameObjects)
            foreach (SkinnedMeshRenderer skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                vertCount += skinnedMeshRenderer.sharedMesh.vertexCount;
                skinnedMeshRendererCount++;
            }

            skinnedMeshRendererBoneCount = skinnedMeshRendererCount * ComputeShaderConstants.BONE_COUNT;
        }

        private void SetupMeshRenderer(List<GameObject> gameObjects, TextureArrayContainer textureArrayContainer, IObjectPool<Material> avatarMaterial, int lastAvatarVertCount, AvatarShapeComponent avatarShapeComponent)
        {
            var auxVertCounter = 0;

            foreach (GameObject gameObject in gameObjects)
            {
                foreach (SkinnedMeshRenderer skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    int currentVertexCount = skinnedMeshRenderer.sharedMesh.vertexCount;
                    Renderer renderer = SetupMesh(skinnedMeshRenderer);
                    renderer.renderingLayerMask = 2;
                    SetupMaterial(renderer, auxVertCounter, textureArrayContainer, avatarMaterial, lastAvatarVertCount, avatarShapeComponent);
                    auxVertCounter += currentVertexCount;
                }
            }
        }

        private Renderer SetupMesh(SkinnedMeshRenderer skin)
        {
            GameObject go = skin.gameObject;
            MeshFilter filter = go.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            filter.mesh = skin.sharedMesh;
            meshRenderer.material = skin.material;
            Object.Destroy(skin);
            return meshRenderer;
        }

        protected override void SetupMaterial(Renderer meshRenderer, int lastWearableVertCount, TextureArrayContainer textureArrayContainer, IObjectPool<Material> celShadingMaterial, int lastAvatarVertCount,
            AvatarShapeComponent avatarShapeComponent)
        {
            Material avatarMaterial = celShadingMaterial.Get();
            Material originalMaterial = meshRenderer.material;
            var albedoTexture = (Texture2D)originalMaterial.mainTexture;

            if (albedoTexture != null)
            {
                UsedTextureArraySlot usedIndex = textureArrayContainer.SetTexture(avatarMaterial, albedoTexture, ComputeShaderConstants.TextureArrayType.ALBEDO);
                usedTextureArraySlots.Add(usedIndex);
            }

            foreach (string keyword in ComputeShaderConstants.keywordsToCheck)
            {
                if (meshRenderer.material.IsKeywordEnabled(keyword))
                    avatarMaterial.EnableKeyword(keyword);
            }

            // HACK: We currently aren't using normal maps so we're just creating shading issues by using this variant.
            avatarMaterial.DisableKeyword("_NORMALMAP");

            //vertOutMaterial.SetColor(ComputeShaderHelpers._BaseColour_ShaderID, Color.red);
            avatarMaterial.SetInteger("_useCompute", 1);
            avatarMaterial.SetInteger(ComputeShaderConstants.LAST_AVATAR_VERT_COUNT_ID, lastWearableVertCount);
            avatarMaterial.SetInteger(ComputeShaderConstants.LAST_WEARABLE_VERT_COUNT_ID, lastAvatarVertCount);

            SetAvatarColors(avatarMaterial, originalMaterial, avatarShapeComponent);
            meshRenderer.material = avatarMaterial;
        }

        public new void Dispose()
        {
            //foreach (UsedTextureArraySlot usedTextureArraySlot in usedTextureArraySlots)
            //    textureArrayContainer.FreeTexture(usedTextureArraySlot);

            usedTextureArraySlots.Clear();
        }
    }
}
