using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.ECSComponents;
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
using DCL.Utility;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using Font = UnityEngine.Font;

namespace DCL.PluginSystem.World
{
    public class SceneUIPlugin : IDCLWorldPlugin<SceneUIPlugin.Settings>
    {
        // Deploy timestamp on/after which an unset textWrap follows the SDK default (wrap); earlier deployments keep
        // the legacy "unset textWrap => no wrap" layout. An explicit textWrap is always honored. (2026-08-11T00:00:00Z)
        private static readonly long TEXT_WRAP_DEFAULT_CUTOFF_MS = new DateTimeOffset(2026, 8, 11, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly FrameTimeCapBudget frameTimeBudgetProvider;
        private readonly MemoryBudget memoryBudgetProvider;
        private readonly IComponentPool<UITransformComponent> transformsPool;
        private readonly IInputBlock inputBlock;
        private readonly bool isLocalSceneDevelopment;

        private UIDocument uiDocument = null!;
        private StyleFontDefinition[] styleFontDefinitions = null!;

        public SceneUIPlugin(ECSWorldSingletonSharedDependencies singletonSharedDependencies, IAssetsProvisioner assetsProvisioner, IInputBlock inputBlock, ILaunchMode launchMode)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.inputBlock = inputBlock;
            isLocalSceneDevelopment = launchMode.CurrentMode is LaunchMode.LocalSceneDevelopment;
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

        /// <summary>
        ///     Whether a text whose textWrap is unset should wrap by default. Locally developed scenes are authored
        ///     against the current SDK default and always wrap; deployed scenes only wrap when they were deployed on or
        ///     after <see cref="TEXT_WRAP_DEFAULT_CUTOFF_MS" />, so older layouts keep the legacy no-wrap behavior.
        ///     A missing timestamp (0) is treated as a legacy deployment.
        /// </summary>
        public static bool ShouldWrapUnsetTextByDefault(bool isLocalSceneDevelopment, long sceneDeployTimestampMs) =>
            isLocalSceneDevelopment || sceneDeployTimestampMs >= TEXT_WRAP_DEFAULT_CUTOFF_MS;

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            uiDocument = (await assetsProvisioner.ProvideInstanceAsync(settings.ScenesUIDocument, ct: ct)).Value;

            uiDocument.rootVisualElement.AddToClassList("sceneUIMainCanvas");
            uiDocument.rootVisualElement.pickingMode = PickingMode.Ignore;

            // Order according to Protocol's TEXT PB message:
            // https://github.com/decentraland/protocol/blob/d6cccca48449239e4b17a4f32bc6d65c44446b43/proto/decentraland/sdk/components/common/texts.proto#L17
            styleFontDefinitions = new[]
            {
                new StyleFontDefinition(settings.FontSansSerif),
                new StyleFontDefinition(settings.FontSerif),
                new StyleFontDefinition(settings.FontMonospace)
            };
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in SystemsDependencies systemsDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
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
            bool wrapUnsetTextByDefault = ShouldWrapUnsetTextByDefault(isLocalSceneDevelopment, sharedDependencies.SceneData.SceneEntityDefinition.timestamp);
            UITextInstantiationSystem.InjectToWorld(ref builder, componentPoolsRegistry, styleFontDefinitions, wrapUnsetTextByDefault);
            UITextReleaseSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            UIBackgroundInstantiationSystem.InjectToWorld(ref builder, componentPoolsRegistry, sharedDependencies.SceneData, frameTimeBudgetProvider, memoryBudgetProvider);
            finalizeWorldSystems.Add(UIBackgroundReleaseSystem.InjectToWorld(ref builder, componentPoolsRegistry));
            UIInputInstantiationSystem.InjectToWorld(ref builder, componentPoolsRegistry, sharedDependencies.EcsToCRDTWriter, inputBlock, styleFontDefinitions);
            UIInputReleaseSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            UIDropdownInstantiationSystem.InjectToWorld(ref builder, componentPoolsRegistry, sharedDependencies.EcsToCRDTWriter, styleFontDefinitions);
            UIDropdownReleaseSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            UIPointerEventsSystem.InjectToWorld(ref builder, sharedDependencies.SceneStateProvider, sharedDependencies.EcsToCRDTWriter);
            UICanvasInformationSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter);
            UIFixPbPointerEventsSystem.InjectToWorld(ref builder);

            ResetDirtyFlagSystem<PBUiTransform>.InjectToWorld(ref builder);
            ResetDirtyFlagSystem<PBUiBackground>.InjectToWorld(ref builder);
            finalizeWorldSystems.Add(ReleasePoolableComponentSystem<Label, UITextComponent>.InjectToWorld(ref builder, componentPoolsRegistry));
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField] public UIDocumentRef ScenesUIDocument { get; private set; } = null!;
            [field: SerializeField] public Font FontSansSerif { get; private set; } = null!;
            [field: SerializeField] public Font FontSerif { get; private set; } = null!;
            [field: SerializeField] public Font FontMonospace { get; private set; } = null!;
        }
    }
}
