using DCL.AvatarRendering.AvatarShape.Rendering.Avatar;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    public class ComputeShaderCustomSkinning : CustomSkinning
    {
        private int skinnedMeshRendererBoneCount;
        private int kernel;
        private int skinnedMeshRendererCount;
        private int vertCount;

        private UnityEngine.ComputeShader cs;
        private ComputeBuffer mBones;

        private readonly List<UsedTextureArraySlot> usedTextureArraySlots;

        private readonly NativeArray<byte> preallocatedArray;


        public ComputeShaderCustomSkinning()
        {
            usedTextureArraySlots = new List<UsedTextureArraySlot>();
            preallocatedArray = new NativeArray<byte>(100000 * 16 * 3, Allocator.Persistent);
        }

        public override void ComputeSkinning(NativeArray<float4x4> bonesResult)
        {
            mBones.SetData(bonesResult);
            cs.Dispatch(kernel, (vertCount / 64) + 1, 1, 1);
        }

        public override int Initialize(List<GameObject> gameObjects, Transform[] bones, TextureArrayContainer textureArrayContainer,
            UnityEngine.ComputeShader skinningShader, Material avatarMaterial, int lastAvatarVertCount, SkinnedMeshRenderer baseAvatarSkinnedMeshRenderer)
        {
            SetupCounters(gameObjects);
            SetupComputeShader(gameObjects, bones, skinningShader, lastAvatarVertCount);
            SetupMeshRenderer(gameObjects, textureArrayContainer, avatarMaterial, lastAvatarVertCount, baseAvatarSkinnedMeshRenderer);
            return vertCount;
        }

        private unsafe NativeArray<T> SliceNativeArray<T>(int start, int size) where T: struct
        {
            int marshalSize = Marshal.SizeOf<T>();
            NativeArray<T> newArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(preallocatedArray.Slice(start * marshalSize, (start + size) * marshalSize).GetUnsafePtr(), size, Allocator.Temp);
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref newArray, AtomicSafetyHandle.Create());
            return newArray;
        }

        private void SetupComputeShader(List<GameObject> gameObjects, Transform[] bones, UnityEngine.ComputeShader skinningShader, int lastAvatarVertCount)
        {
            //Setting up pool arrays
            NativeArray<Vector3> totalVertsIn = SliceNativeArray<Vector3>(0, vertCount);
            NativeArray<Vector3> totalNormalsIn = SliceNativeArray<Vector3>(vertCount, vertCount);
            NativeArray<Vector4> totalTangentsIn = SliceNativeArray<Vector4>(vertCount * 2, vertCount);
            NativeArray<BoneWeight> totalSkinIn = SliceNativeArray<BoneWeight>(vertCount * 4, vertCount);

            //var bindPosesIndexList = SliceNativeArray<int>(vertCount * 5, vertCount);
            var bindPosesIndexList = new NativeArray<int>(vertCount, Allocator.Temp);
            NativeArray<Matrix4x4> bindPosesMatrix = SliceNativeArray<Matrix4x4>(vertCount * 6, skinnedMeshRendererBoneCount);

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

            SetupBuffers(bones, totalVertsIn, totalNormalsIn, totalTangentsIn, totalSkinIn, bindPosesMatrix, bindPosesIndexList, skinningShader, lastAvatarVertCount);
        }

        private void SetupBuffers(Transform[] bones, NativeArray<Vector3> vertsIn, NativeArray<Vector3> normsIn, NativeArray<Vector4> tangIn, NativeArray<BoneWeight> totalSkinIn,
            NativeArray<Matrix4x4> bindPosesMatrix, NativeArray<int> bindPosesIndexList, UnityEngine.ComputeShader skinningShader, int lastAvatarVertCount)
        {
            //TODO: Find out why adding ComputeBufferType.Constant doesnt work in Windows, but it does in Mac
            var vertexIn = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(Vector3)));
            vertexIn.SetData(vertsIn);
            var tangentsIn = new ComputeBuffer(vertCount, Marshal.SizeOf(typeof(Vector4)));
            tangentsIn.SetData(tangIn);
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
            kernel = cs.FindKernel(ComputeShaderHelpers.SKINNING_KERNEL_NAME);
            cs.SetInt(ComputeShaderHelpers.VERT_COUNT_ID, vertCount);
            cs.SetInt(ComputeShaderHelpers.LAST_AVATAR_VERT_COUNT_ID, lastAvatarVertCount);
            cs.SetBuffer(kernel, ComputeShaderHelpers.VERTS_IN_ID, vertexIn);
            cs.SetBuffer(kernel, ComputeShaderHelpers.NORMALS_IN_ID, normalsIn);
            cs.SetBuffer(kernel, ComputeShaderHelpers.TANGENTS_IN_ID, tangentsIn);
            cs.SetBuffer(kernel, ComputeShaderHelpers.SOURCE_SKIN_ID, sourceSkin);
            cs.SetBuffer(kernel, ComputeShaderHelpers.BIND_POSE_ID, bindPoses);
            cs.SetBuffer(kernel, ComputeShaderHelpers.BIND_POSES_INDEX_ID, bindPosesIndex);
            cs.SetBuffer(kernel, ComputeShaderHelpers.BONES_ID, mBones);
        }

        private void FillMeshArray(SkinnedMeshRenderer skinnedMeshRenderer, NativeArray<Matrix4x4> bindPosesMatrix,
            NativeArray<int> bindPosesIndexList, NativeArray<Vector3> totalVertsIn, NativeArray<Vector3> totalNormalsIn, NativeArray<Vector4> totalTangentsIn, NativeArray<BoneWeight> totalSkinIn,
            int currentMeshVertexCount, int vertexCounter, int skinnedMeshCounter)
        {
            Mesh mesh = skinnedMeshRenderer.sharedMesh;

            mesh.RecalculateTangents();
            //mesh.RecalculateNormals();

            NativeArray<Matrix4x4>.Copy(mesh.bindposes, 0, bindPosesMatrix, ComputeShaderHelpers.BONE_COUNT * skinnedMeshCounter, ComputeShaderHelpers.BONE_COUNT);
            NativeArray<BoneWeight>.Copy(mesh.boneWeights, 0, totalSkinIn, vertexCounter, currentMeshVertexCount);
            NativeArray<Vector3>.Copy(mesh.vertices, 0, totalVertsIn, vertexCounter, currentMeshVertexCount);
            NativeArray<Vector3>.Copy(mesh.normals, 0, totalNormalsIn, vertexCounter, currentMeshVertexCount);
            NativeArray<Vector4>.Copy(mesh.tangents, 0, totalTangentsIn, vertexCounter, currentMeshVertexCount);

            //Setup vertex index for current wearable
            for (var i = 0; i < mesh.vertexCount; i++)
                bindPosesIndexList[vertexCounter + i] = ComputeShaderHelpers.BONE_COUNT * skinnedMeshCounter;
        }

        private void SetupCounters(List<GameObject> gameObjects)
        {
            foreach (GameObject gameObject in gameObjects)
            foreach (SkinnedMeshRenderer skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                vertCount += skinnedMeshRenderer.sharedMesh.vertexCount;
                skinnedMeshRendererCount++;
            }

            skinnedMeshRendererBoneCount = skinnedMeshRendererCount * ComputeShaderHelpers.BONE_COUNT;
        }

        private void SetupMeshRenderer(List<GameObject> gameObjects, TextureArrayContainer textureArrayContainer, Material avatarMaterial, int lastAvatarVertCount, SkinnedMeshRenderer baseAvatarSkinnedMeshRenderer)
        {
            var auxVertCounter = 0;

            foreach (GameObject gameObject in gameObjects)
            {
                foreach (SkinnedMeshRenderer skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    int currentVertexCount = skinnedMeshRenderer.sharedMesh.vertexCount;
                    Renderer renderer = SetupMesh(skinnedMeshRenderer, baseAvatarSkinnedMeshRenderer);
                    SetupMaterial(renderer, auxVertCounter, textureArrayContainer, avatarMaterial, lastAvatarVertCount);
                    auxVertCounter += currentVertexCount;
                }
            }
        }

        private Renderer SetupMesh(SkinnedMeshRenderer skin, SkinnedMeshRenderer baseAvatarSkinnedMeshRenderer)
        {
            GameObject go = skin.gameObject;
            MeshFilter filter = go.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            filter.mesh = skin.sharedMesh;
            meshRenderer.material = skin.material;
            Object.Destroy(skin);
            return meshRenderer;
        }

        protected override void SetupMaterial(Renderer meshRenderer, int lastWearableVertCount, TextureArrayContainer textureArrayContainer, Material celShadingMaterial, int lastAvatarVertCount)
        {
            var vertOutMaterial = new Material(celShadingMaterial);

            var albedoTexture = (Texture2D)meshRenderer.material.mainTexture;

            if (albedoTexture != null)
            {
                UsedTextureArraySlot usedIndex = textureArrayContainer.SetTexture(vertOutMaterial, albedoTexture, ComputeShaderHelpers.TextureArrayType.ALBEDO);
                usedTextureArraySlots.Add(usedIndex);
            }

            foreach (string keyword in ComputeShaderHelpers.keywordsToCheck)
            {
                if (meshRenderer.material.IsKeywordEnabled(keyword))
                    vertOutMaterial.EnableKeyword(keyword);
            }

            //vertOutMaterial.SetColor(ComputeShaderHelpers._BaseColour_ShaderID, Color.red);
            meshRenderer.material = vertOutMaterial;
            vertOutMaterial.SetInteger("_useCompute", 1);
            vertOutMaterial.SetInteger(ComputeShaderHelpers.LAST_AVATAR_VERT_COUNT_ID, lastWearableVertCount);
            vertOutMaterial.SetInteger(ComputeShaderHelpers.LAST_WEARABLE_VERT_COUNT_ID, lastAvatarVertCount);
        }

        public new void Dispose()
        {
            //foreach (UsedTextureArraySlot usedTextureArraySlot in usedTextureArraySlots)
            //    textureArrayContainer.FreeTexture(usedTextureArraySlot);

            usedTextureArraySlots.Clear();
        }
    }
}
