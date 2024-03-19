using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.AvatarRendering.Wearables.Helpers;
using System.Collections.Generic;
using DCL.AvatarRendering.AvatarShape.Helpers;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    public abstract class CustomSkinning
    {
        private static readonly int baseColor = Shader.PropertyToID("_BaseColor");

        public abstract AvatarCustomSkinningComponent Initialize(IList<CachedWearable> gameObjects,
            UnityEngine.ComputeShader skinningShader, IAvatarMaterialPoolHandler avatarMaterial,
            AvatarShapeComponent avatarShapeComponent, Dictionary<string, Texture> facialFeatureTexture);

        public abstract void ComputeSkinning(NativeArray<float4x4> bonesResult, ref AvatarCustomSkinningComponent skinning);

        private protected abstract AvatarCustomSkinningComponent.MaterialSetup SetupMaterial(Renderer meshRenderer, Material originalMaterial, int lastWearableVertCount, IAvatarMaterialPoolHandler celShadingMaterial,
            AvatarShapeComponent shapeComponent, Dictionary<string, Texture> facialFeatureTexture);

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

            avatarMaterial.SetInt("_CullMode", (int)originalMaterial.GetFloat("_Cull"));
        }
    }
}
