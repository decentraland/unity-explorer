using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Rendering.Avatar;
using DCL.AvatarRendering.Wearables.Helpers;
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

        public abstract int Initialize(IReadOnlyList<CachedWearable> gameObjects, TextureArrayContainer textureArrayContainer,
            UnityEngine.ComputeShader skinningShader, IObjectPool<Material> avatarMaterial, int lastAvatarVertCount, SkinnedMeshRenderer baseAvatarSkinnedMeshRenderer, AvatarShapeComponent avatarShapeComponent);

        protected abstract void SetupMaterial(Renderer meshRenderer, int lastWearableVertCount, TextureArrayContainer textureArrayContainer, IObjectPool<Material> avatarMaterialPool, int lastAvatarVertCount,
            AvatarShapeComponent avatarShapeComponent);

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

        protected void SetAvatarColors(Material avatarMaterial, Material originalMaterial, AvatarShapeComponent avatarShapeComponent)
        {
            // PATO: If this is modified, check DecentralandMaterialGenerator.SetMaterialName,
            // its important for the asset bundles materials to have normalized names but this functionality should work too
            string name = originalMaterial.name.ToLower();

            if (name.Contains("skin"))
                avatarMaterial.SetColor(ComputeShaderConstants._BaseColour_ShaderID, avatarShapeComponent.SkinColor);
            else if (name.Contains("hair"))
                avatarMaterial.SetColor(ComputeShaderConstants._BaseColour_ShaderID, avatarShapeComponent.HairColor);
        }

        public void Dispose() { }
    }
}
