using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.Utilities;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.Common;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using WearablesLoadResult = ECS.StreamableLoading.Common.Components.StreamableLoadingResult<DCL.AvatarRendering.Wearables.Components.WearablesResolution>;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarLoaderSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class AvatarInstantiatorSystem : BaseUnityLoopSystem
    {
        private static readonly HashSet<string> EMPTY_STRING_HASH_SET = new (0);

        private readonly IComponentPool<AvatarBase> avatarPoolRegistry;
        private readonly IAvatarMaterialPoolHandler avatarMaterialPoolHandler;
        private readonly IObjectPool<UnityEngine.ComputeShader> computeShaderSkinningPool;
        private readonly IPerformanceBudget instantiationFrameTimeBudget;
        private readonly CustomSkinning skinningStrategy;
        private readonly FixedComputeBufferHandler vertOutBuffer;
        private readonly IAttachmentsAssetsCache wearableAssetsCache;
        private readonly IPerformanceBudget memoryBudget;
        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;
        private readonly IWearableStorage wearableStorage;
        private readonly IWearable?[] fallbackBodyShape = new IWearable[1];

        private readonly AvatarTransformMatrixJobWrapper avatarTransformMatrixBatchJob;
        private readonly FacialFeaturesTextures[] facialFeaturesTexturesByBodyShape;

        public AvatarInstantiatorSystem(World world, IPerformanceBudget instantiationFrameTimeBudget, IPerformanceBudget memoryBudget,
            IComponentPool<AvatarBase> avatarPoolRegistry, IAvatarMaterialPoolHandler avatarMaterialPoolHandler, IObjectPool<UnityEngine.ComputeShader> computeShaderPool,
            IAttachmentsAssetsCache wearableAssetsCache, CustomSkinning skinningStrategy, FixedComputeBufferHandler vertOutBuffer,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy,
            IWearableStorage wearableStorage,
            AvatarTransformMatrixJobWrapper avatarTransformMatrixBatchJob,
            FacialFeaturesTextures[] facialFeaturesTexturesByBodyShape) : base(world)
        {
            this.instantiationFrameTimeBudget = instantiationFrameTimeBudget;
            this.avatarPoolRegistry = avatarPoolRegistry;

            this.wearableAssetsCache = wearableAssetsCache;
            this.skinningStrategy = skinningStrategy;
            this.vertOutBuffer = vertOutBuffer;
            this.memoryBudget = memoryBudget;
            this.avatarMaterialPoolHandler = avatarMaterialPoolHandler;
            computeShaderSkinningPool = computeShaderPool;
            this.mainPlayerAvatarBaseProxy = mainPlayerAvatarBaseProxy;
            this.wearableStorage = wearableStorage;
            this.avatarTransformMatrixBatchJob = avatarTransformMatrixBatchJob;
            this.facialFeaturesTexturesByBodyShape = facialFeaturesTexturesByBodyShape;
        }

        protected override void OnDispose()
        {
            vertOutBuffer.Dispose();
        }

        protected override void Update(float t)
        {
            InstantiateMainPlayerAvatarQuery(World);
            InstantiateNewAvatarQuery(World);
            InstantiateExistingAvatarQuery(World);
        }

        [Query]
        [None(typeof(PlayerComponent), typeof(AvatarBase), typeof(AvatarTransformMatrixComponent), typeof(AvatarCustomSkinningComponent), typeof(DeleteEntityIntention))]
        private AvatarBase? InstantiateNewAvatar(in Entity entity, ref AvatarShapeComponent avatarShapeComponent, ref CharacterTransform transformComponent)
        {
            if (!ReadyToInstantiateNewAvatar(ref avatarShapeComponent)) return null;

            if (!avatarShapeComponent.WearablePromise.SafeTryConsume(World, GetReportCategory(), out WearablesLoadResult wearablesResult)) return null;

            AvatarBase avatarBase = avatarPoolRegistry.Get();
            avatarBase.gameObject.name = $"Avatar {avatarShapeComponent.ID}";

            Transform avatarTransform = avatarBase.transform;

            if (transformComponent.Transform != null)
            {
                avatarTransform.SetParent(transformComponent.Transform, false);

                using PoolExtensions.Scope<List<Transform>> children = avatarTransform.gameObject.GetComponentsInChildrenIntoPooledList<Transform>(true);

                for (var index = 0; index < children.Value.Count; index++)
                {
                    Transform child = children.Value[index];

                    if (child != null) { child.gameObject.layer = transformComponent.Transform.gameObject.layer; }
                }
            }

            avatarTransform.ResetLocalTRS();

            var avatarTransformMatrixComponent =
                AvatarTransformMatrixComponent.Create(avatarBase.AvatarSkinnedMeshRenderer.bones);

            AvatarCustomSkinningComponent skinningComponent = InstantiateAvatar(ref avatarShapeComponent, in wearablesResult, avatarBase);

            World.Add(entity, avatarBase, (IAvatarView)avatarBase, avatarTransformMatrixComponent, skinningComponent);
            return avatarBase;
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(AvatarBase), typeof(AvatarTransformMatrixComponent), typeof(AvatarCustomSkinningComponent))]
        private void InstantiateMainPlayerAvatar(in Entity entity, ref AvatarShapeComponent avatarShapeComponent, ref CharacterTransform transformComponent)
        {
            var avatarBase = InstantiateNewAvatar(entity, ref avatarShapeComponent, ref transformComponent);

            if (avatarBase != null)
            {
                avatarBase.RigBuilder.enabled = true;
                mainPlayerAvatarBaseProxy.SetObject(avatarBase);
            }
        }

        [Query]
        [All(typeof(CharacterTransform))]
        [None(typeof(DeleteEntityIntention))]
        private void InstantiateExistingAvatar(ref AvatarShapeComponent avatarShapeComponent, AvatarBase avatarBase,
            ref AvatarCustomSkinningComponent skinningComponent,
            ref AvatarTransformMatrixComponent avatarTransformMatrixComponent)
        {
            if (!ReadyToInstantiateNewAvatar(ref avatarShapeComponent)) return;

            if (!avatarShapeComponent.WearablePromise.SafeTryConsume(World, GetReportCategory(), out WearablesLoadResult wearablesResult)) return;

            ReleaseAvatar.Execute(vertOutBuffer, wearableAssetsCache, avatarMaterialPoolHandler,
                computeShaderSkinningPool, avatarShapeComponent, ref skinningComponent,
                ref avatarTransformMatrixComponent, avatarTransformMatrixBatchJob);

            // Override by ref
            skinningComponent = InstantiateAvatar(ref avatarShapeComponent, in wearablesResult, avatarBase);
        }

        private AvatarCustomSkinningComponent InstantiateAvatar(ref AvatarShapeComponent avatarShapeComponent,
            in WearablesLoadResult wearablesResult,
            AvatarBase avatarBase)
        {
            GetWearablesByPointersIntention wearableIntention = avatarShapeComponent.WearablePromise.LoadingIntention;

            HashSet<string> wearablesToHide = wearablesResult.Succeeded ? wearablesResult.Asset.HiddenCategories : EMPTY_STRING_HASH_SET;
            HashSet<string> usedCategories = HashSetPool<string>.Get();

            GameObject? bodyShape = null;

            IList<IWearable> visibleWearables;

            if (wearablesResult.Succeeded)
                visibleWearables = wearablesResult.Asset.Wearables;
            else
            {
                // We need at least a body shape to be able to instantiate an avatar
                // In case the wearable result failed, fallback into a male body shape to get something visible so the flow doesnt get broken
                if (fallbackBodyShape[0] == null)

                    // Could be a very rare case on which the body shape is not available. This case will make the flow fail
                    if (wearableStorage.TryGetElement(BodyShape.MALE, out IWearable maleBody))
                        fallbackBodyShape[0] = maleBody;

                visibleWearables = fallbackBodyShape!;
            }

            var facialFeatureTextures = facialFeaturesTexturesByBodyShape[avatarShapeComponent.BodyShape].Clone();
            var attachPoint = avatarBase.transform;

            for (var i = 0; i < visibleWearables.Count; i++)
            {
                IWearable resultWearable = visibleWearables[i];

                GameObject? instance = resultWearable.AppendToAvatar(wearableAssetsCache, usedCategories, ref facialFeatureTextures, ref avatarShapeComponent, attachPoint);

                if (resultWearable.Type == WearableType.BodyShape)
                    bodyShape = instance;
            }

            WearableComponentsUtils.HideBodyShape(bodyShape, wearablesToHide, usedCategories);
            HashSetPool<string>.Release(usedCategories);

            AvatarCustomSkinningComponent skinningComponent = skinningStrategy.Initialize(avatarShapeComponent.InstantiatedWearables,
                computeShaderSkinningPool.Get(), avatarMaterialPoolHandler, avatarShapeComponent, facialFeatureTextures);

            if (!avatarShapeComponent.IsVisible)
                foreach (CachedAttachment cachedAttachment in avatarShapeComponent.InstantiatedWearables)
                    foreach (var renderer in cachedAttachment.Renderers)
                        renderer.enabled = false;

            skinningStrategy.SetVertOutRegion(vertOutBuffer.Rent(skinningComponent.vertCount), ref skinningComponent);
            avatarBase.gameObject.SetActive(true);

            avatarShapeComponent.CreateOutlineCompatibilityList();
            wearableIntention.Dispose();

            if (wearablesResult.Succeeded)
                wearablesResult.Asset.Dispose();

            avatarShapeComponent.IsDirty = false;

            return skinningComponent;
        }

        private bool ReadyToInstantiateNewAvatar(ref AvatarShapeComponent avatarShapeComponent) =>
            avatarShapeComponent.IsDirty && instantiationFrameTimeBudget.TrySpendBudget() && memoryBudget.TrySpendBudget();
    }
}
