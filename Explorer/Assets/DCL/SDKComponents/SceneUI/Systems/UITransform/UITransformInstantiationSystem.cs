using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using ECS.Abstract;
using ECS.Groups;
using UnityEngine.Pool;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Systems.UITransform
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class UITransformInstantiationSystem : BaseUnityLoopSystem
    {
        private readonly UIDocument canvas;
        private readonly IComponentPool<VisualElement> transformsPool;

        public UITransformInstantiationSystem(World world, UIDocument canvas, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            this.canvas = canvas;
            transformsPool = poolsRegistry.GetReferenceTypePool<VisualElement>();
        }

        protected override void Update(float t)
        {
            InstantiateUITransformQuery(World);
        }

        [Query]
        [All(typeof(PBUiTransform))]
        [None(typeof(UITransformComponent))]
        private void InstantiateUITransform(in Entity entity)
        {
            VisualElement newTransform = transformsPool.Get();
            newTransform.name = $"UITransform (Entity {entity.Id})";
            canvas.rootVisualElement.Add(newTransform);
            var transformComponent = new UITransformComponent();
            transformComponent.Transform = newTransform;
            transformComponent.Parent = EntityReference.Null;
            transformComponent.Children = HashSetPool<EntityReference>.Get();
            transformComponent.IsHidden = false;
            World.Add(entity, transformComponent);
        }
    }
}
