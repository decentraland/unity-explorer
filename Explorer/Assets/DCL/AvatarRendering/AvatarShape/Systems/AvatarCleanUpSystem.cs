using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Loading.Assets;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using UnityEngine.Pool;

namespace DCL.AvatarRendering.AvatarShape
{
    /// <summary>
    ///     The system must be executed last to ensure that `DeleteEntityIntention` is properly handled before entity is destroyed.
    /// </summary>
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class AvatarCleanUpSystem : BaseUnityLoopSystem
    {
        private readonly FixedComputeBufferHandler vertOutBuffer;
        private readonly IAvatarMaterialPoolHandler avatarMaterialPoolHandler;
        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;
        private readonly IComponentPool<AvatarBase> avatarPoolRegistry;
        private readonly IObjectPool<UnityEngine.ComputeShader> computeShaderSkinningPool;
        private readonly IAttachmentsAssetsCache wearableAssetsCache;

        private readonly AvatarTransformMatrixJobWrapper avatarTransformMatrixBatchJob;


        internal AvatarCleanUpSystem(
            World world,
            FixedComputeBufferHandler vertOutBuffer,
            IAvatarMaterialPoolHandler avatarMaterialPoolHandler,
            IComponentPool<AvatarBase> avatarPoolRegistry,
            IObjectPool<UnityEngine.ComputeShader> computeShaderSkinningPool,
            IAttachmentsAssetsCache wearableAssetsCache,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy,
            AvatarTransformMatrixJobWrapper avatarTransformMatrixBatchJob) : base(world)
        {
            this.vertOutBuffer = vertOutBuffer;
            this.avatarMaterialPoolHandler = avatarMaterialPoolHandler;
            this.avatarPoolRegistry = avatarPoolRegistry;
            this.computeShaderSkinningPool = computeShaderSkinningPool;
            this.wearableAssetsCache = wearableAssetsCache;
            this.mainPlayerAvatarBaseProxy = mainPlayerAvatarBaseProxy;
            this.avatarTransformMatrixBatchJob = avatarTransformMatrixBatchJob;
        }

        protected override void Update(float t)
        {
            DestroyAvatarQuery(World);
            DestroyNotInstantiatedAvatarQuery(World);
        }

        protected override void OnDispose()
        {
            DestroyAvatarOnDisposeQuery(World);
        }

        /// <summary>
        /// Release all resources without budgeting
        /// </summary>
        [Query]
        private void DestroyAvatarOnDispose(ref AvatarTransformMatrixComponent avatarTransformMatrixComponent, ref AvatarShapeComponent avatarShapeComponent,
            ref AvatarCustomSkinningComponent skinningComponent, ref AvatarBase avatarBase)
        {
            InternalDestroyAvatar(ref avatarShapeComponent, ref skinningComponent, ref avatarTransformMatrixComponent, avatarBase);
        }

        [Query]
        private void DestroyAvatar(ref AvatarShapeComponent avatarShapeComponent, ref AvatarTransformMatrixComponent avatarTransformMatrixComponent,
            AvatarBase avatarBase, ref AvatarCustomSkinningComponent skinningComponent, ref DeleteEntityIntention deleteEntityIntention)
        {
            deleteEntityIntention.DeferDeletion = false;
            InternalDestroyAvatar(ref avatarShapeComponent, ref skinningComponent, ref avatarTransformMatrixComponent, avatarBase);
        }

        // Releases deleted avatars that have no skinning: deleted mid-load, or stripped after a failed re-instantiation.
        [Query]
        [None(typeof(AvatarCustomSkinningComponent), typeof(AvatarTransformMatrixComponent))]
        private void DestroyNotInstantiatedAvatar(ref AvatarShapeComponent avatarShapeComponent, AvatarBase avatarBase, ref DeleteEntityIntention deleteEntityIntention)
        {
            deleteEntityIntention.DeferDeletion = false;
            avatarShapeComponent.WearablePromise.ForgetLoading(World);
            ReleaseAvatarBase(avatarBase);
        }

        private void InternalDestroyAvatar(ref AvatarShapeComponent avatarShapeComponent,
            ref AvatarCustomSkinningComponent skinningComponent, ref AvatarTransformMatrixComponent avatarTransformMatrixComponent,
            AvatarBase avatarBase)
        {
            ReleaseAvatar.Execute(vertOutBuffer, wearableAssetsCache, avatarMaterialPoolHandler,
                computeShaderSkinningPool, avatarShapeComponent, ref skinningComponent,
                ref avatarTransformMatrixComponent, avatarTransformMatrixBatchJob);

            ReleaseAvatarBase(avatarBase);
        }

        private void ReleaseAvatarBase(AvatarBase avatarBase)
        {
            if (mainPlayerAvatarBaseProxy.Object == avatarBase)
                mainPlayerAvatarBaseProxy.ReleaseObject();

            avatarPoolRegistry.Release(avatarBase);
        }
    }
}
