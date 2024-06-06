using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Classes;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Groups;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.Unity.Textures.Components;
using ECS.Unity.Textures.Components.Extensions;
using SceneRunner.Scene;
using System;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.SDKComponents.SceneUI.Systems.UIBackground
{
    [UpdateInGroup(typeof(SceneUIComponentInstantiationGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    public partial class UIBackgroundInstantiationSystem : BaseUnityLoopSystem
    {
        private const int ATTEMPTS_COUNT = 6;

        private readonly IComponentPool<DCLImage> imagesPool;
        private readonly ISceneData sceneData;
        private readonly IPerformanceBudget frameTimeBudgetProvider;
        private readonly IPerformanceBudget memoryBudgetProvider;

        public UIBackgroundInstantiationSystem(
            World world,
            IComponentPoolsRegistry poolsRegistry,
            ISceneData sceneData,
            IPerformanceBudget frameTimeBudgetProvider,
            IPerformanceBudget memoryBudgetProvider) : base(world)
        {
            imagesPool = poolsRegistry.GetReferenceTypePool<DCLImage>();
            this.sceneData = sceneData;
            this.frameTimeBudgetProvider = frameTimeBudgetProvider;
            this.memoryBudgetProvider = memoryBudgetProvider;
        }

        protected override void Update(float t)
        {
            InstantiateUIBackgroundQuery(World);
            UpdateUIBackgroundQuery(World);
            LoadUIBackgroundTextureQuery(World);
        }

        [Query]
        [All(typeof(PBUiBackground))]
        [None(typeof(UIBackgroundComponent))]
        private void InstantiateUIBackground(in Entity entity, ref UITransformComponent uiTransformComponent)
        {
            var image = imagesPool.Get();
            image.Initialize(uiTransformComponent.Transform);
            var uiBackgroundComponent = new UIBackgroundComponent();
            uiBackgroundComponent.Image = image;
            uiBackgroundComponent.Status = LifeCycle.LoadingNotStarted;
            World.Add(entity, uiBackgroundComponent);
        }

        [Query]
        private void UpdateUIBackground(CRDTEntity sdkEntity, ref PBUiBackground sdkModel, ref UIBackgroundComponent uiBackgroundComponent, ref PartitionComponent partitionComponent)
        {
            if (!sdkModel.IsDirty)
                return;

            // Create texture promise if needed
            TextureComponent? backgroundTexture = sdkModel.Texture.CreateTextureComponent(sceneData);
            TryCreateGetTexturePromise(in backgroundTexture, ref uiBackgroundComponent.TexturePromise, ref partitionComponent);
            uiBackgroundComponent.Status = LifeCycle.LoadingInProgress;

            if (uiBackgroundComponent.TexturePromise == null)
            {
                // Backgrounds without texture
                UiElementUtils.SetupDCLImage(ref uiBackgroundComponent.Image, ref sdkModel);
                uiBackgroundComponent.Status = LifeCycle.LoadingFinished;
            }

            sdkModel.IsDirty = false;
        }

        [Query]
        private void LoadUIBackgroundTexture(ref PBUiBackground sdkModel, ref UIBackgroundComponent uiBackgroundComponent)
        {
            if (uiBackgroundComponent.Status != LifeCycle.LoadingInProgress ||
                uiBackgroundComponent.TexturePromise == null ||
                uiBackgroundComponent.TexturePromise.Value.IsConsumed ||
                !frameTimeBudgetProvider.TrySpendBudget() ||
                !memoryBudgetProvider.TrySpendBudget())
                return;

            var texturePromise = uiBackgroundComponent.TexturePromise.Value;

            if (texturePromise.TryConsume(World, out StreamableLoadingResult<Texture2D> promiseResult))
            {
                if (promiseResult.Succeeded)
                    // Backgrounds with texture
                    UiElementUtils.SetupDCLImage(ref uiBackgroundComponent.Image, ref sdkModel, promiseResult.Asset);
                else
                    ReportHub.LogError(ReportCategory.SCENE_UI, "Error consuming texture promise");
                uiBackgroundComponent.Status = LifeCycle.LoadingFinished;

                // Write value back as it's nullable (and can't be accessed by ref)
                uiBackgroundComponent.TexturePromise = texturePromise;
            }
        }

        private void TryCreateGetTexturePromise(in TextureComponent? textureComponent, ref Promise? promise, ref PartitionComponent partitionComponent)
        {
            if (textureComponent == null)
            {
                // If component is being reuse forget the previous promise
                TryAddAbortIntention(World, ref promise);
                return;
            }

            TextureComponent textureComponentValue = textureComponent.Value;

            // If data inside promise has not changed just reuse the same promise
            // as creating and waiting for a new one can be expensive
            if (Equals(ref textureComponentValue, ref promise))
                return;

            // If component is being reused forget the previous promise
            TryAddAbortIntention(World, ref promise);

            promise = Promise.Create(World, new GetTextureIntention
            {
                CommonArguments = new CommonLoadingArguments(textureComponentValue.Src, attempts: ATTEMPTS_COUNT),
                WrapMode = textureComponentValue.WrapMode,
                FilterMode = textureComponentValue.FilterMode,
            }, partitionComponent);
        }

        private static void TryAddAbortIntention(World world, ref Promise? promise)
        {
            if (promise == null)
                return;

            promise.Value.ForgetLoading(world);

            // Nullify the entity reference
            promise = null;
        }

        private static bool Equals(ref TextureComponent textureComponent, ref Promise? promise)
        {
            if (promise == null)
                return false;

            Promise promiseValue = promise.Value;
            GetTextureIntention intention = promiseValue.LoadingIntention;

            return textureComponent.Src == promiseValue.LoadingIntention.CommonArguments.URL &&
                   textureComponent.WrapMode == intention.WrapMode &&
                   textureComponent.FilterMode == intention.FilterMode;
        }
    }
}
