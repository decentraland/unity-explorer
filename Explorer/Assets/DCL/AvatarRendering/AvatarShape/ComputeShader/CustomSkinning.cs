using DCL.AvatarRendering.AvatarShape.Rendering.Avatar;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    public abstract class CustomSkinning : IDisposable
    {
        public abstract void ComputeSkinning(NativeArray<float4x4> bonesResult);

        public abstract int Initialize(List<GameObject> gameObjects, TextureArrayContainer textureArrayContainer,
            UnityEngine.ComputeShader skinningShader, IObjectPool<Material> avatarMaterial, int lastAvatarVertCount, SkinnedMeshRenderer baseAvatarSkinnedMeshRenderer);

        protected abstract void SetupMaterial(Renderer meshRenderer, int lastWearableVertCount, TextureArrayContainer textureArrayContainer, IObjectPool<Material> avatarMaterialPool, int lastAvatarVertCount);

        protected void ResetTransforms(SkinnedMeshRenderer skinnedMeshRenderer, Transform rootTransform)
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

        public void Dispose()
        {
        }
    }
}
