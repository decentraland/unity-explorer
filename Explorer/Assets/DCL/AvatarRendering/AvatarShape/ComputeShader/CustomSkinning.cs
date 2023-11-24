using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Rendering.Avatar;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.PerformanceAndDiagnostics.Optimization.Pools;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Utility;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    public abstract class CustomSkinning
    {
        public abstract AvatarCustomSkinningComponent Initialize(IReadOnlyList<CachedWearable> gameObjects, TextureArrayContainer textureArrayContainer,
            UnityEngine.ComputeShader skinningShader, IObjectPoolDCL<Material> avatarMaterial,
            SkinnedMeshRenderer baseAvatarSkinnedMeshRenderer,
            AvatarShapeComponent avatarShapeComponent);

        public abstract void ComputeSkinning(NativeArray<float4x4> bonesResult, ref AvatarCustomSkinningComponent skinning);

        private protected abstract AvatarCustomSkinningComponent.MaterialSetup SetupMaterial(Renderer meshRenderer, Material originalMaterial, int lastWearableVertCount, TextureArrayContainer textureArrayContainer, IObjectPoolDCL<Material> celShadingMaterial,
            AvatarShapeComponent shapeComponent);

        public abstract void SetVertOutRegion(FixedComputeBufferHandler.Slice region, ref AvatarCustomSkinningComponent skinningComponent);

        protected void ResetTransforms(Transform currentTransform, Transform rootTransform)
        {
            // Make sure that Transform is uniform with the root
            // Non-uniform does not make sense as skin relatively to the base avatar
            // so we just waste calculations on transformation matrices

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

            if (name.Contains(ComputeShaderConstants.SKIN_MATERIAL_NAME))
                avatarMaterial.SetColor(ComputeShaderConstants._BaseColour_ShaderID, avatarShapeComponent.SkinColor);
            else if (name.Contains(ComputeShaderConstants.HAIR_MATERIAL_NAME))
                avatarMaterial.SetColor(ComputeShaderConstants._BaseColour_ShaderID, avatarShapeComponent.HairColor);
        }
    }
}
