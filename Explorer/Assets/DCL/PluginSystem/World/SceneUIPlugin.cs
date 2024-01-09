using Arch.SystemGroups;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle;
using System.Collections.Generic;
using UnityEngine.UIElements;
using UITextInstantiationSystem = DCL.SDKComponents.SceneUI.Systems.UIText.UITextInstantiationSystem;
using UITextReleaseSystem = DCL.SDKComponents.SceneUI.Systems.UIText.UITextReleaseSystem;
using UITransformInstantiationSystem = DCL.SDKComponents.SceneUI.Systems.UITransform.UITransformInstantiationSystem;
using UITransformParentingSystem = DCL.SDKComponents.SceneUI.Systems.UITransform.UITransformParentingSystem;
using UITransformReleaseSystem = DCL.SDKComponents.SceneUI.Systems.UITransform.UITransformReleaseSystem;
using UITransformSortingSystem = DCL.SDKComponents.SceneUI.Systems.UITransform.UITransformSortingSystem;
using UITransformUpdateSystem = DCL.SDKComponents.SceneUI.Systems.UITransform.UITransformUpdateSystem;

namespace DCL.PluginSystem.World
{
    public class SceneUIPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly UIDocument canvas;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public SceneUIPlugin(
            UIDocument canvas,
            StyleSheet canvasStyleSheet,
            ECSWorldSingletonSharedDependencies singletonSharedDependencies)
        {
            this.canvas = canvas;
            if (this.canvas.rootVisualElement != null)
            {
                this.canvas.rootVisualElement.styleSheets.Add(canvasStyleSheet);
                this.canvas.rootVisualElement.AddToClassList("sceneUIMainCanvas");
                this.canvas.rootVisualElement.pickingMode = PickingMode.Ignore;
            }

            componentPoolsRegistry = singletonSharedDependencies.ComponentPoolsRegistry;
            componentPoolsRegistry.AddComponentPool<VisualElement>(onRelease: UiElementUtils.ReleaseUIElement);
            componentPoolsRegistry.AddComponentPool<Label>(onRelease: UiElementUtils.ReleaseUIElement);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            UITransformInstantiationSystem.InjectToWorld(ref builder, canvas, componentPoolsRegistry);
            UITransformParentingSystem.InjectToWorld(ref builder, sharedDependencies.EntitiesMap, persistentEntities.SceneRoot);
            UITransformSortingSystem.InjectToWorld(ref builder, sharedDependencies.EntitiesMap, persistentEntities.SceneRoot);
            UITransformUpdateSystem.InjectToWorld(ref builder, canvas, sharedDependencies.SceneStateProvider);
            UITransformReleaseSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            UITextInstantiationSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            UITextReleaseSystem.InjectToWorld(ref builder, componentPoolsRegistry);

            finalizeWorldSystems.Add(ReleasePoolableComponentSystem<VisualElement, UITransformComponent>.InjectToWorld(ref builder, componentPoolsRegistry));
            finalizeWorldSystems.Add(ReleasePoolableComponentSystem<Label, UITextComponent>.InjectToWorld(ref builder, componentPoolsRegistry));
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}
