using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Input;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.SceneUI.Classes;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Systems;
using DCL.SDKComponents.SceneUI.Systems.UIBackground;
using DCL.SDKComponents.SceneUI.Systems.UICanvasInformation;
using DCL.SDKComponents.SceneUI.Systems.UIDropdown;
using DCL.SDKComponents.SceneUI.Systems.UIInput;
using DCL.SDKComponents.SceneUI.Systems.UIPointerEvents;
using DCL.SDKComponents.SceneUI.Systems.UIText;
using DCL.SDKComponents.SceneUI.Systems.UITransform;
using DCL.SDKComponents.SceneUI.Utils;
using DCL.Utilities;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace DCL.PluginSystem.World
{
    public class SceneUIPlugin : IDCLWorldPlugin<SceneUIPlugin.Settings>
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly FrameTimeCapBudget frameTimeBudgetProvider;
        private readonly MemoryBudget memoryBudgetProvider;
        private readonly IComponentPool<UITransformComponent> transformsPool;
        private readonly IInputBlock inputBlock;

        private UIDocument uiDocument = null!;

        public SceneUIPlugin(ECSWorldSingletonSharedDependencies singletonSharedDependencies, IAssetsProvisioner assetsProvisioner, IInputBlock inputBlock)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.inputBlock = inputBlock;
            componentPoolsRegistry = singletonSharedDependencies.ComponentPoolsRegistry;
            transformsPool = componentPoolsRegistry.AddComponentPool<UITransformComponent>(onRelease: UiElementUtils.ReleaseUITransformComponent, maxSize: 200);
            componentPoolsRegistry.AddComponentPool<Label>(onRelease: UiElementUtils.ReleaseUIElement, maxSize: 100);
            componentPoolsRegistry.AddComponentPool<DCLImage>(onRelease: UiElementUtils.ReleaseDCLImage, maxSize: 100);
            componentPoolsRegistry.AddComponentPool<UIInputComponent>(onRelease: UiElementUtils.ReleaseUIInputComponent, maxSize: 50);
            componentPoolsRegistry.AddComponentPool<UIDropdownComponent>(onRelease: UiElementUtils.ReleaseUIDropdownComponent, maxSize: 50);

            frameTimeBudgetProvider = singletonSharedDependencies.FrameTimeBudget;
            memoryBudgetProvider = singletonSharedDependencies.MemoryBudget;
        }

        public void Dispose()
        {
        }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            uiDocument = (await assetsProvisioner.ProvideInstanceAsync(settings.ScenesUIDocument, ct: ct)).Value;

            uiDocument.rootVisualElement.AddToClassList("sceneUIMainCanvas");
            uiDocument.rootVisualElement.pickingMode = PickingMode.Ignore;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            // Add a regular UITransformComponent to the root entity so we can treat with the common scheme
            UITransformComponent? rootUiTransform = transformsPool.Get();
            rootUiTransform.InitializeAsRoot(uiDocument.rootVisualElement);
            builder.World.Add(persistentEntities.SceneRoot, rootUiTransform);

            UITransformInstantiationSystem.InjectToWorld(ref builder, uiDocument, componentPoolsRegistry);
            UITransformParentingSystem.InjectToWorld(ref builder, sharedDependencies.EntitiesMap, persistentEntities.SceneRoot);
            UITransformSortingSystem.InjectToWorld(ref builder, sharedDependencies.EntitiesMap);
            sceneIsCurrentListeners.Add(UITransformUpdateSystem.InjectToWorld(ref builder, uiDocument, sharedDependencies.SceneStateProvider, persistentEntities.SceneRoot));
            UITransformReleaseSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            UITextInstantiationSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            UITextReleaseSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            UIBackgroundInstantiationSystem.InjectToWorld(ref builder, componentPoolsRegistry, sharedDependencies.SceneData, frameTimeBudgetProvider, memoryBudgetProvider);
            finalizeWorldSystems.Add(UIBackgroundReleaseSystem.InjectToWorld(ref builder, componentPoolsRegistry));
            UIInputInstantiationSystem.InjectToWorld(ref builder, componentPoolsRegistry, sharedDependencies.EcsToCRDTWriter, inputBlock);
            UIInputReleaseSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            UIDropdownInstantiationSystem.InjectToWorld(ref builder, componentPoolsRegistry, sharedDependencies.EcsToCRDTWriter);
            UIDropdownReleaseSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            UIPointerEventsSystem.InjectToWorld(ref builder, sharedDependencies.SceneStateProvider, sharedDependencies.EcsToCRDTWriter);
            UICanvasInformationSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter);
            UIFixPbPointerEventsSystem.InjectToWorld(ref builder);

            finalizeWorldSystems.Add(ReleasePoolableComponentSystem<Label, UITextComponent>.InjectToWorld(ref builder, componentPoolsRegistry));
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField] public UIDocumentRef ScenesUIDocument { get; private set; } = null!;
        }
    }
}
