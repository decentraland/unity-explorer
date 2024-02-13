using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.AvatarShape.Rendering.Avatar;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using StreamableResult = ECS.StreamableLoading.Common.Components.StreamableLoadingResult<DCL.AvatarRendering.Wearables.Components.WearablesResolution>;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(AvatarLoaderSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class AvatarInstantiatorSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<AvatarBase> avatarPoolRegistry;
        private readonly IObjectPool<Material> avatarMaterialPool;
        private readonly IObjectPool<UnityEngine.ComputeShader> computeShaderSkinningPool;
        private readonly IPerformanceBudget instantiationFrameTimeBudget;

        private readonly CustomSkinning skinningStrategy;

        private readonly TextureArrayContainer textureArrays;
        private readonly FixedComputeBufferHandler vertOutBuffer;
        private readonly IWearableAssetsCache wearableAssetsCache;
        private readonly IPerformanceBudget memoryBudget;

        private readonly MainPlayerAvatarBase mainPlayerAvatarBase;

        public AvatarInstantiatorSystem(World world, IPerformanceBudget instantiationFrameTimeBudget, IPerformanceBudget memoryBudget,
            IComponentPool<AvatarBase> avatarPoolRegistry, IObjectPool<Material> avatarMaterialPool, IObjectPool<UnityEngine.ComputeShader> computeShaderPool,
            TextureArrayContainer textureArrayContainer, IWearableAssetsCache wearableAssetsCache, CustomSkinning skinningStrategy, FixedComputeBufferHandler vertOutBuffer, MainPlayerAvatarBase mainPlayerAvatarBase) : base(world)
        {
            this.instantiationFrameTimeBudget = instantiationFrameTimeBudget;
            this.avatarPoolRegistry = avatarPoolRegistry;

            textureArrays = textureArrayContainer;
            this.wearableAssetsCache = wearableAssetsCache;
            this.skinningStrategy = skinningStrategy;
            this.vertOutBuffer = vertOutBuffer;
            this.memoryBudget = memoryBudget;
            this.avatarMaterialPool = avatarMaterialPool;
            computeShaderSkinningPool = computeShaderPool;
            this.mainPlayerAvatarBase = mainPlayerAvatarBase;
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
        private void InstantiateNewAvatar(in Entity entity, ref AvatarShapeComponent avatarShapeComponent, ref CharacterTransform transformComponent)
        {
            if (!ReadyToInstantiateNewAvatar(ref avatarShapeComponent)) return;

            if (!avatarShapeComponent.WearablePromise.TryConsume(World, out StreamableResult wearablesResult)) return;

            AvatarBase avatarBase = avatarPoolRegistry.Get();
            avatarBase.gameObject.name = $"Avatar {avatarShapeComponent.ID}";

            Transform avatarTransform = avatarBase.transform;

            if (transformComponent.Transform != null)
            {
                avatarTransform.SetParent(transformComponent.Transform, false);

                PoolExtensions.Scope<List<Transform>> children = avatarTransform.gameObject.GetComponentsInChildrenIntoPooledList<Transform>(true);

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
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(AvatarBase), typeof(AvatarTransformMatrixComponent), typeof(AvatarCustomSkinningComponent))]
        private void InstantiateMainPlayerAvatar(in Entity entity, ref AvatarShapeComponent avatarShapeComponent, ref CharacterTransform transformComponent)
        {
            InstantiateNewAvatar(entity, ref avatarShapeComponent, ref transformComponent);

            if (World.TryGet(entity, out AvatarBase avatarBase))
                mainPlayerAvatarBase.SetAvatarBase(avatarBase);
        }

        [Query]
        [All(typeof(CharacterTransform))]
        private void InstantiateExistingAvatar(ref AvatarShapeComponent avatarShapeComponent, AvatarBase avatarBase,
            ref AvatarCustomSkinningComponent skinningComponent)
        {
            if (!ReadyToInstantiateNewAvatar(ref avatarShapeComponent)) return;

            if (!avatarShapeComponent.WearablePromise.TryConsume(World, out StreamableResult wearablesResult)) return;

            CommonAvatarRelease(avatarShapeComponent, skinningComponent);

            // Override by ref
            skinningComponent = InstantiateAvatar(ref avatarShapeComponent, wearablesResult, avatarBase);
        }

        private AvatarCustomSkinningComponent InstantiateAvatar(ref AvatarShapeComponent avatarShapeComponent, in StreamableResult wearablesResult, AvatarBase avatarBase)
        {
            GetWearablesByPointersIntention intention = avatarShapeComponent.WearablePromise.LoadingIntention;

            HashSet<string> wearablesToHide = wearablesResult.Asset.HiddenCategories;
            HashSet<string> usedCategories = HashSetPool<string>.Get();

            GameObject bodyShape = null;

            List<IWearable> visibleWearables = wearablesResult.Asset.Wearables;

            for (var i = 0; i < visibleWearables.Count; i++)
            {
                IWearable resultWearable = visibleWearables[i];

                if (resultWearable.isFacialFeature())
                {
                    //TODO: Facial Features. They are textures that should be applied on the body shape, not gameobjects to instantiate.
                    //We need the asset bundle to have access to the texture
                }
                else
                {
                    WearableAsset originalAsset = resultWearable.GetOriginalAsset(avatarShapeComponent.BodyShape);

                    if (originalAsset.GameObject == null)
                    {
                        ReportHub.LogError(GetReportCategory(),
                            $"Wearable asset {resultWearable.GetUrn()} has no GameObject! Check the Asset bundle generated.");

                        continue;
                    }

                    CachedWearable instantiatedWearable =
                        wearableAssetsCache.InstantiateWearable(originalAsset, avatarBase.transform);

                    avatarShapeComponent.InstantiatedWearables.Add(instantiatedWearable);

                    usedCategories.Add(resultWearable.GetCategory());

                    if (resultWearable.IsBodyShape())
                        bodyShape = instantiatedWearable;
                }
            }

            AvatarWearableHide.HideBodyShape(bodyShape, wearablesToHide, usedCategories);
            HashSetPool<string>.Release(usedCategories);

            AvatarCustomSkinningComponent skinningComponent = skinningStrategy.Initialize(avatarShapeComponent.InstantiatedWearables,
                textureArrays, computeShaderSkinningPool.Get(), avatarMaterialPool, avatarBase.AvatarSkinnedMeshRenderer, avatarShapeComponent);

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
            skinningComponent.Dispose(avatarMaterialPool, computeShaderSkinningPool);

            if (avatarShapeComponent.WearablePromise.IsConsumed)
                wearableAssetsCache.ReleaseAssets(avatarShapeComponent.InstantiatedWearables);
            else
                foreach (IWearable wearable in avatarShapeComponent.WearablePromise.Result.Value.Asset.Wearables)
                    wearable?.WearableAssetResults[avatarShapeComponent.BodyShape]?.Asset.Dereference();
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

        private void InternalDestroyAvatar(ref AvatarShapeComponent avatarShapeComponent,
            ref AvatarCustomSkinningComponent skinningComponent, ref AvatarTransformMatrixComponent avatarTransformMatrixComponent,
            AvatarBase avatarBase)
        {
            CommonAvatarRelease(avatarShapeComponent, skinningComponent);
            avatarTransformMatrixComponent.Dispose();
            avatarPoolRegistry.Release(avatarBase);
        }
    }
}
