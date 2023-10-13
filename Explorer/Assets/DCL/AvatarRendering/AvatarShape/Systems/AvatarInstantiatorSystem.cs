using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.AvatarShape.Rendering.Avatar;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.ComponentsPooling;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Promise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.IWearable[], DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(AvatarLoaderSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class AvatarInstantiatorSystem : BaseUnityLoopSystem
    {
        private readonly IObjectPool<Material> avatarMaterialPool;
        private readonly IComponentPool<AvatarBase> avatarPoolRegistry;
        private readonly IObjectPool<UnityEngine.ComputeShader> computeShaderSkinningPool;
        private readonly IConcurrentBudgetProvider instantiationFrameTimeBudgetProvider;

        private readonly CustomSkinning skinningStrategy;

        private readonly TextureArrayContainer textureArrays;
        private readonly FixedComputeBufferHandler vertOutBuffer;
        private readonly IWearableAssetsCache wearableAssetsCache;

        public AvatarInstantiatorSystem(World world, IConcurrentBudgetProvider instantiationFrameTimeBudgetProvider,
            IComponentPool<AvatarBase> avatarPoolRegistry, IObjectPool<Material> avatarMaterialPool, IObjectPool<UnityEngine.ComputeShader> computeShaderPool, TextureArrayContainer textureArrayContainer,
            IWearableAssetsCache wearableAssetsCache, CustomSkinning skinningStrategy, FixedComputeBufferHandler vertOutBuffer) : base(world)
        {
            this.instantiationFrameTimeBudgetProvider = instantiationFrameTimeBudgetProvider;
            this.avatarPoolRegistry = avatarPoolRegistry;

            textureArrays = textureArrayContainer;
            this.wearableAssetsCache = wearableAssetsCache;
            this.skinningStrategy = skinningStrategy;
            this.vertOutBuffer = vertOutBuffer;
            this.avatarMaterialPool = avatarMaterialPool;
            computeShaderSkinningPool = computeShaderPool;
        }

        protected override void Update(float t)
        {
            InstantiateNewAvatarQuery(World);
            InstantiateExistingAvatarQuery(World);
            DestroyAvatarQuery(World);
        }


        [Query]
        [All(typeof(TransformComponent))]
        private void InstantiateExistingAvatar(ref AvatarShapeComponent avatarShapeComponent, AvatarBase avatarBase,
            ref AvatarCustomSkinningComponent skinningComponent)
        {
            if (!ReadyToInstantiateNewAvatar(ref avatarShapeComponent)) return;

            if (!avatarShapeComponent.WearablePromise.TryConsume(World, out StreamableLoadingResult<IWearable[]> wearablesResult)) return;

            CommonAvatarRelease(avatarShapeComponent, skinningComponent);

            // Override by ref
            skinningComponent = InstantiateAvatar(ref avatarShapeComponent, wearablesResult, avatarBase);
        }

        [Query]
        [None(typeof(AvatarBase), typeof(AvatarTransformMatrixComponent), typeof(AvatarCustomSkinningComponent))]
        private void InstantiateNewAvatar(in Entity entity, ref AvatarShapeComponent avatarShapeComponent, ref TransformComponent transformComponent)
        {
            if (!ReadyToInstantiateNewAvatar(ref avatarShapeComponent)) return;

            if (!avatarShapeComponent.WearablePromise.TryConsume(World, out StreamableLoadingResult<IWearable[]> wearablesResult)) return;

            AvatarBase avatarBase = avatarPoolRegistry.Get();
            avatarBase.gameObject.name = $"Avatar {avatarShapeComponent.ID}";

            Transform avatarTransform = avatarBase.transform;
            avatarTransform.SetParent(transformComponent.Transform, false);
            avatarTransform.ResetLocalTRS();

            //Debug stuff, remove after demo
            avatarBase.SetAsMainPlayer(avatarShapeComponent.Name.Equals("Player"));

            var avatarTransformMatrixComponent = AvatarTransformMatrixComponent.Create(avatarBase.transform, avatarBase.AvatarSkinnedMeshRenderer.bones);

            AvatarCustomSkinningComponent skinningComponent = InstantiateAvatar(ref avatarShapeComponent, wearablesResult, avatarBase);

            World.Add(entity, avatarBase, avatarTransformMatrixComponent, skinningComponent);
        }

        private AvatarCustomSkinningComponent InstantiateAvatar(ref AvatarShapeComponent avatarShapeComponent,
            StreamableLoadingResult<IWearable[]> wearablesResult, AvatarBase avatarBase)
        {
            GetWearablesByPointersIntention intention = avatarShapeComponent.WearablePromise.LoadingIntention;

            HashSet<string> wearablesToHide = HashSetPool<string>.Get();
            HashSet<string> usedCategories = HashSetPool<string>.Get();

            AvatarWearableHide.ComposeHiddenCategoriesOrdered(avatarShapeComponent.BodyShape, null, wearablesResult.Asset,
                intention.Pointers.Count, wearablesToHide);

            GameObject bodyShape = null;

            //Using Pointer size for counter, since we dont know the size of the results array
            //because it was pooled
            for (var i = 0; i < intention.Pointers.Count; i++)
            {
                IWearable resultWearable = wearablesResult.Asset[i];

                if (wearablesToHide.Contains(resultWearable.GetCategory()))
                    continue;

                if (resultWearable.isFacialFeature())
                {
                    //TODO: Facial Features. They are textures that should be applied on the body shape, not gameobjects to instantiate.
                    //We need the asset bundle to have access to the texture
                }
                else
                {
                    WearableAsset originalAsset = resultWearable.GetOriginalAsset(avatarShapeComponent.BodyShape);

                    CachedWearable instantiatedWearable =
                        wearableAssetsCache.InstantiateWearable(originalAsset, avatarBase.transform);

                    avatarShapeComponent.InstantiatedWearables.Add(instantiatedWearable);

                    usedCategories.Add(resultWearable.GetCategory());

                    if (resultWearable.IsBodyShape())
                        bodyShape = instantiatedWearable;
                }
            }

            AvatarWearableHide.HideBodyShape(bodyShape, wearablesToHide, usedCategories);
            HashSetPool<string>.Release(wearablesToHide);
            HashSetPool<string>.Release(usedCategories);

            AvatarCustomSkinningComponent skinningComponent = skinningStrategy.Initialize(avatarShapeComponent.InstantiatedWearables,
                textureArrays, computeShaderSkinningPool.Get(), avatarMaterialPool, avatarBase.AvatarSkinnedMeshRenderer, avatarShapeComponent);

            skinningStrategy.SetVertOutRegion(vertOutBuffer.Rent(skinningComponent.vertCount), ref skinningComponent);
            avatarBase.gameObject.SetActive(true);

            intention.Dispose();
            avatarShapeComponent.IsDirty = false;

            return skinningComponent;
        }

        private bool ReadyToInstantiateNewAvatar(ref AvatarShapeComponent avatarShapeComponent) =>
            avatarShapeComponent.IsDirty && instantiationFrameTimeBudgetProvider.TrySpendBudget();

        //We only care to release AvatarShapeComponent with AvatarBase; since this are the ones that got instantiated.
        //The ones that dont have AvatarBase never got instantiated and therefore we have nothing to release
        [Query]
        private void DestroyAvatar(ref AvatarShapeComponent avatarShapeComponent, ref AvatarTransformMatrixComponent avatarTransformMatrixComponent,
            AvatarBase avatarBase, AvatarCustomSkinningComponent skinningComponent, ref DeleteEntityIntention deleteEntityIntention)
        {
            // Use frame budget for destruction as well
            if (!instantiationFrameTimeBudgetProvider.TrySpendBudget())
            {
                avatarBase.gameObject.SetActive(false);
                deleteEntityIntention.DeferDeletion = true;
                return;
            }

            InternalDestroyAvatar(ref avatarShapeComponent, ref skinningComponent, ref avatarTransformMatrixComponent, avatarBase);
            deleteEntityIntention.DeferDeletion = false;
        }

        private void CommonAvatarRelease(AvatarShapeComponent avatarShapeComponent, AvatarCustomSkinningComponent skinningComponent)
        {
            vertOutBuffer.Release(skinningComponent.VertsOutRegion);
            skinningComponent.Dispose(avatarMaterialPool);
            wearableAssetsCache.TryReleaseAssets(avatarShapeComponent.InstantiatedWearables, avatarMaterialPool);
        }


        public override void Dispose()
        {
            World.Query(in new QueryDescription().WithAll<AvatarBase, AvatarTransformMatrixComponent, AvatarShapeComponent, AvatarCustomSkinningComponent>(),
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
