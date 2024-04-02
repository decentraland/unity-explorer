using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
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
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using WearablesLoadResult = ECS.StreamableLoading.Common.Components.StreamableLoadingResult<DCL.AvatarRendering.Wearables.Components.WearablesResolution>;
using EmotesLoadResult = ECS.StreamableLoading.Common.Components.StreamableLoadingResult<DCL.AvatarRendering.Emotes.Components.EmotesResolution>;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(AvatarLoaderSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class AvatarInstantiatorSystem : BaseUnityLoopSystem
    {
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

        public AvatarInstantiatorSystem(World world, IPerformanceBudget instantiationFrameTimeBudget, IPerformanceBudget memoryBudget,
            IComponentPool<AvatarBase> avatarPoolRegistry, IAvatarMaterialPoolHandler avatarMaterialPoolHandler, IObjectPool<UnityEngine.ComputeShader> computeShaderPool,
            IWearableAssetsCache wearableAssetsCache, CustomSkinning skinningStrategy, FixedComputeBufferHandler vertOutBuffer, ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy, IDefaultFaceFeaturesHandler defaultFaceFeaturesHandler) : base(world)
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

            WearablesLoadResult wearablesResult;

            if (!avatarShapeComponent.WearablePromise.IsConsumed)
            {
                if (!avatarShapeComponent.WearablePromise.TryConsume(World, out wearablesResult)) return null;
            }
            else
                wearablesResult = avatarShapeComponent.WearablePromise.Result!.Value;

            EmotesLoadResult emotesResult;

            if (!avatarShapeComponent.EmotePromise.IsConsumed)
            {
                if (!avatarShapeComponent.EmotePromise.TryConsume(World, out emotesResult)) return null;
            }
            else
                emotesResult = avatarShapeComponent.EmotePromise.Result!.Value;

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

            AvatarCustomSkinningComponent skinningComponent = InstantiateAvatar(ref avatarShapeComponent, in wearablesResult, in emotesResult, avatarBase);

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

            if (!avatarShapeComponent.WearablePromise.TryConsume(World, out WearablesLoadResult wearablesResult)) return;
            if (!avatarShapeComponent.EmotePromise.TryConsume(World, out EmotesLoadResult emotesResult)) return;

            CommonAvatarRelease(avatarShapeComponent, skinningComponent);

            // Override by ref
            skinningComponent = InstantiateAvatar(ref avatarShapeComponent, in wearablesResult, in emotesResult, avatarBase);
        }

        private AvatarCustomSkinningComponent InstantiateAvatar(ref AvatarShapeComponent avatarShapeComponent,
            in WearablesLoadResult wearablesResult,

            // TODO: do something with the emotes result
            in EmotesLoadResult streamableLoadingResult,
            AvatarBase avatarBase)
        {
            GetWearablesByPointersIntention intention = avatarShapeComponent.WearablePromise.LoadingIntention;

            HashSet<string> wearablesToHide = wearablesResult.Asset.HiddenCategories;
            HashSet<string> usedCategories = HashSetPool<string>.Get();

            GameObject bodyShape = null;

            List<IWearable> visibleWearables = wearablesResult.Asset.Wearables;

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

            intention.Dispose();
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
