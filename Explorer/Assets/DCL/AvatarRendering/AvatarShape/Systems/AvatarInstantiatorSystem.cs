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
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Promise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.IWearable[], DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class AvatarInstantiatorSystem : BaseUnityLoopSystem
    {
        private readonly IConcurrentBudgetProvider instantiationFrameTimeBudgetProvider;
        private readonly IComponentPool<AvatarBase> avatarPoolRegistry;

        private readonly TextureArrayContainer textureArrays;
        private readonly IObjectPool<Material> avatarMaterialPool;
        private readonly IObjectPool<UnityEngine.ComputeShader> computeShaderSkinningPool;
        private readonly IWearableAssetsCache wearableAssetsCache;

        private readonly ComputeBuffer vertexOutBuffer;

        private int lastAvatarVertCount;

        public struct VertexInfo
        {
            public Vector3 position;
            public Vector3 normal;
            public Vector4 tangent;

        }

        public AvatarInstantiatorSystem(World world, IConcurrentBudgetProvider instantiationFrameTimeBudgetProvider,
            IComponentPool<AvatarBase> avatarPoolRegistry, IObjectPool<Material> avatarMaterialPool, IObjectPool<UnityEngine.ComputeShader> computeShaderPool, TextureArrayContainer textureArrayContainer,
            IWearableAssetsCache wearableAssetsCache) : base(world)
        {
            this.instantiationFrameTimeBudgetProvider = instantiationFrameTimeBudgetProvider;
            this.avatarPoolRegistry = avatarPoolRegistry;

            //TODO: This generates a MASSIVE hiccup. We could hide it behind the loading screen, but we should be careful
            textureArrays = textureArrayContainer;
            this.wearableAssetsCache = wearableAssetsCache;
            this.avatarMaterialPool = avatarMaterialPool;
            computeShaderSkinningPool = computeShaderPool;

            //TODO: Looks like it needs to be released
            vertexOutBuffer = new ComputeBuffer(5_000_000, Unsafe.SizeOf<VertexInfo>());
            Shader.SetGlobalBuffer("_GlobalAvatarBuffer", vertexOutBuffer);
        }

        protected override void Update(float t)
        {
            InstantiateNewAvatarQuery(World);
            InstantiateExistingAvatarQuery(World);
            DestroyAvatarQuery(World);
            UpdateAvatarBonesQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateAvatarBones(ref AvatarShapeComponent avatarShapeComponent, ref AvatarTransformMatrixComponent avatarTransformMatrixComponent, ComputeShaderSkinning computeShaderSkinning)
        {
            if (avatarShapeComponent.IsDirty)
                return;

            computeShaderSkinning.ComputeSkinning(avatarTransformMatrixComponent.CompleteBoneMatrixCalculations());
        }


        [Query]
        public void InstantiateExistingAvatar(ref AvatarShapeComponent avatarShapeComponent, ref TransformComponent transformComponent, AvatarBase avatarBase, ComputeShaderSkinning computeSkinningComponent)
        {
            if (!ReadyToInstantiateNewAvatar(ref avatarShapeComponent)) return;

            if (!avatarShapeComponent.WearablePromise.TryConsume(World, out StreamableLoadingResult<IWearable[]> wearablesResult)) return;

            computeSkinningComponent.Dispose();
            wearableAssetsCache.TryReleaseAssets(avatarShapeComponent.InstantiatedWearables);

            InstantiateAvatar(ref avatarShapeComponent, wearablesResult, avatarBase, computeSkinningComponent);
        }


        [Query]
        [None(typeof(AvatarBase), typeof(AvatarTransformMatrixComponent), typeof(ComputeShaderSkinning))]
        public void InstantiateNewAvatar(in Entity entity, ref AvatarShapeComponent avatarShapeComponent, ref TransformComponent transformComponent)
        {
            if (!ReadyToInstantiateNewAvatar(ref avatarShapeComponent)) return;

            if (!avatarShapeComponent.WearablePromise.TryConsume(World, out StreamableLoadingResult<IWearable[]> wearablesResult)) return;

            AvatarBase avatarBase = avatarPoolRegistry.Get();
            avatarBase.gameObject.name = $"Avatar {avatarShapeComponent.ID}";

            Transform avatarTransform = avatarBase.transform;
            avatarTransform.SetParent(transformComponent.Transform, false);
            avatarTransform.ResetLocalTRS();

            //TODO: Debug stuff, remove after demo
            avatarBase.SetAsMainPlayer(avatarShapeComponent.Name.Equals("Player"));

            var avatarTransformMatrixComponent = AvatarTransformMatrixComponent.Create();
            ;
            avatarTransformMatrixComponent.SetupBurstJob(avatarBase.transform, avatarBase.AvatarSkinnedMeshRenderer.bones);

            var computeShaderSkinning = new ComputeShaderSkinning();

            InstantiateAvatar(ref avatarShapeComponent, wearablesResult, avatarBase, computeShaderSkinning);

            World.Add(entity, avatarBase, avatarTransformMatrixComponent, computeShaderSkinning);
        }

        private void InstantiateAvatar(ref AvatarShapeComponent avatarShapeComponent,
            StreamableLoadingResult<IWearable[]> wearablesResult, AvatarBase avatarBase, ComputeShaderSkinning computeShaderSkinning)
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

            int newVertCount = computeShaderSkinning.Initialize(avatarShapeComponent.InstantiatedWearables,
                textureArrays, computeShaderSkinningPool.Get(), avatarMaterialPool, lastAvatarVertCount, avatarBase.AvatarSkinnedMeshRenderer, avatarShapeComponent);
            lastAvatarVertCount += newVertCount;

            intention.Dispose();
            avatarShapeComponent.IsDirty = false;
        }

        private bool ReadyToInstantiateNewAvatar(ref AvatarShapeComponent avatarShapeComponent) =>
            avatarShapeComponent.IsDirty && instantiationFrameTimeBudgetProvider.TrySpendBudget();

        //We only care to release AvatarShapeComponent with AvatarBase; since this are the ones that got instantiated.
        //The ones that dont have AvatarBase never got instantiated and therefore we have nothing to release
        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void DestroyAvatar(ref AvatarShapeComponent avatarShapeComponent, ref AvatarTransformMatrixComponent avatarTransformMatrixComponent, AvatarBase avatarBase, ComputeShaderSkinning computeSkinningComponent)
        {
            // Use frame budget for destruction as well
            if (!instantiationFrameTimeBudgetProvider.TrySpendBudget())
                return;

            avatarPoolRegistry.Release(avatarBase);
            avatarTransformMatrixComponent.Dispose();
            computeSkinningComponent.Dispose();

            wearableAssetsCache.TryReleaseAssets(avatarShapeComponent.InstantiatedWearables);
        }

        public override void Dispose()
        {
            base.Dispose();
            vertexOutBuffer.Release();
        }
    }
}
