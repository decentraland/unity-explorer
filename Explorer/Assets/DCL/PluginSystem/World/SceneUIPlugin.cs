using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.SceneUI.Classes;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Systems.UIBackground;
using DCL.SDKComponents.SceneUI.Systems.UICanvasInformation;
using DCL.SDKComponents.SceneUI.Systems.UIDropdown;
using DCL.SDKComponents.SceneUI.Systems.UIInput;
using DCL.SDKComponents.SceneUI.Systems.UIPointerEvents;
using DCL.SDKComponents.SceneUI.Systems.UIText;
using DCL.SDKComponents.SceneUI.Systems.UITransform;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.PluginSystem.World
{
    public class SceneUIPlugin : IDCLWorldPlugin<SceneUIPlugin.Settings>
    {
        private UIDocument? canvas;

        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly FrameTimeCapBudget frameTimeBudgetProvider;
        private readonly MemoryBudget memoryBudgetProvider;
        private readonly IComponentPool<UITransformComponent> transformsPool;

        public SceneUIPlugin(ECSWorldSingletonSharedDependencies singletonSharedDependencies, IAssetsProvisioner assetsProvisioner)
        {
            this.assetsProvisioner = assetsProvisioner;
            componentPoolsRegistry = singletonSharedDependencies.ComponentPoolsRegistry;
            transformsPool = componentPoolsRegistry.AddComponentPool<UITransformComponent>(onRelease: UiElementUtils.ReleaseUITransformComponent, maxSize: 200)!;
            componentPoolsRegistry.AddComponentPool<Label>(onRelease: UiElementUtils.ReleaseUIElement, maxSize: 100);
            componentPoolsRegistry.AddComponentPool<DCLImage>(onRelease: UiElementUtils.ReleaseDCLImage, maxSize: 100);
            componentPoolsRegistry.AddComponentPool<UIInputComponent>(onRelease: UiElementUtils.ReleaseUIInputComponent, maxSize: 50);
            componentPoolsRegistry.AddComponentPool<UIDropdownComponent>(onRelease: UiElementUtils.ReleaseUIDropdownComponent, maxSize: 50);

            frameTimeBudgetProvider = singletonSharedDependencies.FrameTimeBudget;
            memoryBudgetProvider = singletonSharedDependencies.MemoryBudget;
        }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            canvas = (await assetsProvisioner.ProvideInstanceAsync(settings.Canvas, ct: ct)).Value;
            StyleSheet scenesUIStyleSheet = (await assetsProvisioner.ProvideMainAssetAsync(settings.StyleSheet, ct)).Value;

            canvas.rootVisualElement!.styleSheets.Add(scenesUIStyleSheet);
            canvas.rootVisualElement.AddToClassList("sceneUIMainCanvas");
            canvas.rootVisualElement.pickingMode = PickingMode.Ignore;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            // Add a regular UITransformComponent to the root entity so we can treat with the common scheme
            var rootUiTransform = transformsPool.Get();
            rootUiTransform.InitializeAsRoot(canvas!.rootVisualElement);
            builder.World.Add(persistentEntities.SceneRoot, rootUiTransform);

            UITransformInstantiationSystem.InjectToWorld(ref builder, canvas, componentPoolsRegistry);
            UITransformParentingSystem.InjectToWorld(ref builder, sharedDependencies.EntitiesMap, persistentEntities.SceneRoot);
            UITransformSortingSystem.InjectToWorld(ref builder, sharedDependencies.EntitiesMap);
            sceneIsCurrentListeners.Add(UITransformUpdateSystem.InjectToWorld(ref builder, canvas, sharedDependencies.SceneStateProvider, persistentEntities.SceneRoot));
            UITransformReleaseSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            UITextInstantiationSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            UITextReleaseSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            UIBackgroundInstantiationSystem.InjectToWorld(ref builder, componentPoolsRegistry, sharedDependencies.SceneData, frameTimeBudgetProvider, memoryBudgetProvider);
            UIBackgroundReleaseSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            UIInputInstantiationSystem.InjectToWorld(ref builder, componentPoolsRegistry, sharedDependencies.EcsToCRDTWriter);
            UIInputReleaseSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            UIDropdownInstantiationSystem.InjectToWorld(ref builder, componentPoolsRegistry, sharedDependencies.EcsToCRDTWriter);
            UIDropdownReleaseSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            UIPointerEventsSystem.InjectToWorld(ref builder, sharedDependencies.SceneStateProvider, sharedDependencies.EcsToCRDTWriter);
            UICanvasInformationSystem.InjectToWorld(ref builder, sharedDependencies.SceneStateProvider, sharedDependencies.EcsToCRDTWriter);

            finalizeWorldSystems.Add(ReleasePoolableComponentSystem<Label, UITextComponent>.InjectToWorld(ref builder, componentPoolsRegistry));
            finalizeWorldSystems.Add(ReleasePoolableComponentSystem<DCLImage, UIBackgroundComponent>.InjectToWorld(ref builder, componentPoolsRegistry));
        }

        public void Dispose() { }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: Header(nameof(SceneUIPlugin) + "." + nameof(Settings))]
            [field: Space]
            [field: SerializeField]
            public UIDocumentRef Canvas { get; private set; } = null!;

            [field: SerializeField]
            public AssetReferenceStyleSheet StyleSheet { get; private set; } = null!;
        }
    }
}
