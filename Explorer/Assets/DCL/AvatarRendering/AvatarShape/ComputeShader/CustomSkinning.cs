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
        public abstract AvatarCustomSkinningComponent Initialize(IList<CachedWearable> gameObjects,
            UnityEngine.ComputeShader skinningShader, IAvatarMaterialPoolHandler avatarMaterial,
            AvatarShapeComponent avatarShapeComponent, in FacialFeaturesTextures facialFeatureTexture);

        public abstract void ComputeSkinning(NativeArray<float4x4> bonesResult, ref AvatarCustomSkinningComponent skinning);

        private protected abstract AvatarCustomSkinningComponent.MaterialSetup SetupMaterial(Renderer meshRenderer, Material originalMaterial, int lastWearableVertCount, IAvatarMaterialPoolHandler celShadingMaterial,
            AvatarShapeComponent shapeComponent, in FacialFeaturesTextures facialFeaturesTextures);

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
    }
}
