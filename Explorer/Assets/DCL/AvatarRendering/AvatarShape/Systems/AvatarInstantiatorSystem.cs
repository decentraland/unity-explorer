using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables;
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
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using WearablesLoadResult = ECS.StreamableLoading.Common.Components.StreamableLoadingResult<DCL.AvatarRendering.Wearables.Components.WearablesResolution>;
using EmotesLoadResult = ECS.StreamableLoading.Common.Components.StreamableLoadingResult<DCL.AvatarRendering.Emotes.EmotesResolution>;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
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
        private readonly TextureArrayContainer textureArrays;
        private readonly FixedComputeBufferHandler vertOutBuffer;
        private readonly IWearableAssetsCache wearableAssetsCache;
        private readonly IPerformanceBudget memoryBudget;
        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;
        private readonly IDefaultFaceFeaturesHandler defaultFaceFeaturesHandler;
        private readonly IWearableCatalog wearableCatalog;
        private readonly IWearable?[] fallbackBodyShape = new IWearable[1];

        public AvatarInstantiatorSystem(World world, IPerformanceBudget instantiationFrameTimeBudget, IPerformanceBudget memoryBudget,
            IComponentPool<AvatarBase> avatarPoolRegistry, IAvatarMaterialPoolHandler avatarMaterialPoolHandler, IObjectPool<UnityEngine.ComputeShader> computeShaderPool,
            IWearableAssetsCache wearableAssetsCache, CustomSkinning skinningStrategy, FixedComputeBufferHandler vertOutBuffer,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy, IDefaultFaceFeaturesHandler defaultFaceFeaturesHandler,
            IWearableCatalog wearableCatalog) : base(world)
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
            this.defaultFaceFeaturesHandler = defaultFaceFeaturesHandler;
            this.wearableCatalog = wearableCatalog;
        }

        public override void Dispose()
        {
            World.Query(in new QueryDescription().WithAll<AvatarBase, AvatarTransformMatrixComponent, AvatarShapeComponent, AvatarCustomSkinningComponent>().WithNone<DeleteEntityIntention>(),
                (ref AvatarTransformMatrixComponent avatarTransformMatrixComponent, ref AvatarShapeComponent avatarShapeComponent, ref AvatarCustomSkinningComponent skinningComponent, ref AvatarBase avatarBase)
                    =>
                {
                    InternalDestroyAvatar(ref avatarShapeComponent, ref skinningComponent, ref avatarTransformMatrixComponent, avatarBase);
                });

            vertOutBuffer.Dispose();
        }

        protected override void Update(float t)
        {
            InstantiateMainPlayerAvatarQuery(World);
            InstantiateNewAvatarQuery(World);
            InstantiateExistingAvatarQuery(World);
            DestroyAvatarQuery(World);
        }

        [Query]
        [None(typeof(PlayerComponent), typeof(AvatarBase), typeof(AvatarTransformMatrixComponent), typeof(AvatarCustomSkinningComponent))]
        private AvatarBase? InstantiateNewAvatar(in Entity entity, ref AvatarShapeComponent avatarShapeComponent, ref CharacterTransform transformComponent)
        {
            if (!ReadyToInstantiateNewAvatar(ref avatarShapeComponent)) return null;

            if (!avatarShapeComponent.WearablePromise.SafeTryConsume(World, out WearablesLoadResult wearablesResult)) return null;
            if (!avatarShapeComponent.EmotePromise.SafeTryConsume(World, out EmotesLoadResult emotesResult)) return null;

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

            var avatarTransformMatrixComponent = AvatarTransformMatrixComponent.Create(avatarBase.transform, avatarBase.AvatarSkinnedMeshRenderer.bones);

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
                mainPlayerAvatarBaseProxy.SetObject(avatarBase);
        }

        [Query]
        [All(typeof(CharacterTransform))]
        private void InstantiateExistingAvatar(ref AvatarShapeComponent avatarShapeComponent, AvatarBase avatarBase,
            ref AvatarCustomSkinningComponent skinningComponent)
        {
            if (!ReadyToInstantiateNewAvatar(ref avatarShapeComponent)) return;

            if (!avatarShapeComponent.WearablePromise.SafeTryConsume(World, out WearablesLoadResult wearablesResult)) return;
            if (!avatarShapeComponent.EmotePromise.SafeTryConsume(World, out EmotesLoadResult emotesResult)) return;

            CommonAvatarRelease(avatarShapeComponent, skinningComponent);

            // Override by ref
            skinningComponent = InstantiateAvatar(ref avatarShapeComponent, in wearablesResult, avatarBase);
        }

        private AvatarCustomSkinningComponent InstantiateAvatar(ref AvatarShapeComponent avatarShapeComponent,
            in WearablesLoadResult wearablesResult,
            AvatarBase avatarBase)
        {
            GetWearablesByPointersIntention wearableIntention = avatarShapeComponent.WearablePromise.LoadingIntention;
            GetEmotesByPointersIntention emoteIntention = avatarShapeComponent.EmotePromise.LoadingIntention;

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
                    if (wearableCatalog.TryGetWearable(BodyShape.MALE, out IWearable maleBody))
                        fallbackBodyShape[0] = maleBody;

                visibleWearables = fallbackBodyShape!;
            }

            var facialFeatureTexture = defaultFaceFeaturesHandler.GetDefaultFacialFeaturesDictionary(avatarShapeComponent.BodyShape);

            var attachPoint = avatarBase.transform;

            for (var i = 0; i < visibleWearables.Count; i++)
            {
                IWearable resultWearable = visibleWearables[i];

                var instance = resultWearable.AppendToAvatar(wearableAssetsCache, usedCategories, ref facialFeatureTexture, ref avatarShapeComponent, attachPoint, GetReportCategory());

                if (resultWearable.Type == WearableType.BodyShape)
                    bodyShape = instance;
            }

            AvatarWearableHide.HideBodyShape(bodyShape, wearablesToHide, usedCategories);
            HashSetPool<string>.Release(usedCategories);

            AvatarCustomSkinningComponent skinningComponent = skinningStrategy.Initialize(avatarShapeComponent.InstantiatedWearables,
                computeShaderSkinningPool.Get(), avatarMaterialPoolHandler, avatarShapeComponent, facialFeatureTexture);

            skinningStrategy.SetVertOutRegion(vertOutBuffer.Rent(skinningComponent.vertCount), ref skinningComponent);
            avatarBase.gameObject.SetActive(true);

            wearableIntention.Dispose();
            emoteIntention.Dispose();

            if (wearablesResult.Succeeded)
                wearablesResult.Asset.Dispose();

            avatarShapeComponent.IsDirty = false;

            return skinningComponent;
        }

        private bool ReadyToInstantiateNewAvatar(ref AvatarShapeComponent avatarShapeComponent) =>
            avatarShapeComponent.IsDirty && instantiationFrameTimeBudget.TrySpendBudget() && memoryBudget.TrySpendBudget();

        //We only care to release AvatarShapeComponent with AvatarBase; since this are the ones that got instantiated.
        //The ones that dont have AvatarBase never got instantiated and therefore we have nothing to release
        [Query]
        private void DestroyAvatar(ref AvatarShapeComponent avatarShapeComponent, ref AvatarTransformMatrixComponent avatarTransformMatrixComponent,
            AvatarBase avatarBase, AvatarCustomSkinningComponent skinningComponent, ref DeleteEntityIntention deleteEntityIntention)
        {
            // Use frame budget for destruction as well
            if (!instantiationFrameTimeBudget.TrySpendBudget())
            {
                avatarBase.gameObject.SetActive(false);
                deleteEntityIntention.DeferDeletion = true;
                return;
            }

            InternalDestroyAvatar(ref avatarShapeComponent, ref skinningComponent, ref avatarTransformMatrixComponent, avatarBase);
            deleteEntityIntention.DeferDeletion = false;
        }

        private void CommonAvatarRelease(in AvatarShapeComponent avatarShapeComponent, AvatarCustomSkinningComponent skinningComponent)
        {
            vertOutBuffer.Release(skinningComponent.VertsOutRegion);
            skinningComponent.Dispose(avatarMaterialPoolHandler, computeShaderSkinningPool);

            if (avatarShapeComponent.WearablePromise.IsConsumed)
                wearableAssetsCache.ReleaseAssets(avatarShapeComponent.InstantiatedWearables);
            else
                avatarShapeComponent.Dereference();
        }

        private void InternalDestroyAvatar(ref AvatarShapeComponent avatarShapeComponent,
            ref AvatarCustomSkinningComponent skinningComponent, ref AvatarTransformMatrixComponent avatarTransformMatrixComponent,
            AvatarBase avatarBase)
        {
            if (mainPlayerAvatarBaseProxy.Object == avatarBase)
                mainPlayerAvatarBaseProxy.ReleaseObject();

            CommonAvatarRelease(avatarShapeComponent, skinningComponent);
            avatarTransformMatrixComponent.Dispose();
            avatarPoolRegistry.Release(avatarBase);
        }
    }
}
