using Arch.SystemGroups;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Systems;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace DCL.PluginSystem.World
{
    public class SceneUIPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly UIDocument canvas;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public SceneUIPlugin(
            UIDocument canvas,
            ECSWorldSingletonSharedDependencies singletonSharedDependencies)
        {
            this.canvas = canvas;
            SetupCanvas();

            componentPoolsRegistry = singletonSharedDependencies.ComponentPoolsRegistry;
            componentPoolsRegistry.AddComponentPool<Label>();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            UITextHandlerSystem.InjectToWorld(ref builder, canvas, componentPoolsRegistry);

            finalizeWorldSystems.Add(ReleasePoolableComponentSystem<Label, UITextComponent>.InjectToWorld(ref builder, componentPoolsRegistry));
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }

        private void SetupCanvas()
        {
            canvas.rootVisualElement.pickingMode = PickingMode.Ignore;

            var style = canvas.rootVisualElement.style;
            style.width = new Length(100f, LengthUnit.Percent);
            style.height = new Length(100f, LengthUnit.Percent);
            style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Row);
            style.flexBasis = new StyleLength(StyleKeyword.Auto);
            style.flexGrow = 0;
            style.flexShrink = 1;
            style.flexWrap = new StyleEnum<Wrap>(Wrap.NoWrap);
            style.justifyContent = new StyleEnum<Justify>(Justify.FlexStart);
            style.alignItems = new StyleEnum<Align>(Align.Stretch);
            style.alignSelf = new StyleEnum<Align>(Align.Auto);
            style.alignContent = new StyleEnum<Align>(Align.Stretch);
            style.position = new StyleEnum<Position>(Position.Absolute);
        }
    }
}
