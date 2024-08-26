﻿using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.Wearables.Helpers;
using UnityEngine.Pool;

namespace DCL.AvatarRendering.AvatarShape.Helpers
{
    public static class ReleaseAvatar
    {
        public static void Execute(FixedComputeBufferHandler vertOutBuffer, IWearableAssetsCache wearableAssetsCache,
            IAvatarMaterialPoolHandler avatarMaterialPoolHandler,
            IObjectPool<UnityEngine.ComputeShader> computeShaderSkinningPool,
            in AvatarShapeComponent avatarShapeComponent, ref AvatarCustomSkinningComponent skinningComponent,
            ref AvatarTransformMatrixComponent avatarTransformMatrixComponent,
            AvatarTransformMatrixJobWrapper jobWrapper)
        {
            vertOutBuffer.Release(skinningComponent.VertsOutRegion);
            skinningComponent.Dispose(avatarMaterialPoolHandler, computeShaderSkinningPool);

            jobWrapper.ReleaseAvatar(ref avatarTransformMatrixComponent);

            if (avatarShapeComponent.WearablePromise.IsConsumed)
                wearableAssetsCache.ReleaseAssets(avatarShapeComponent.InstantiatedWearables);
            else
                avatarShapeComponent.Dereference();
        }
    }
}
