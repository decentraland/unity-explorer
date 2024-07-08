using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CRDT;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Defaults;
using DCL.Utilities.Extensions;
using ECS.Abstract;
using ECS.Groups;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Systems.UITransform
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class UITransformInstantiationSystem : BaseUnityLoopSystem
    {
        private const string COMPONENT_NAME = "UITransform";

        private readonly UIDocument canvas;
        private readonly IComponentPool<UITransformComponent> transformsPool;

        public UITransformInstantiationSystem(World world, UIDocument canvas,
            IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            this.canvas = canvas;
            transformsPool = poolsRegistry.GetReferenceTypePool<UITransformComponent>().EnsureNotNull();
        }

        protected override void Update(float t)
        {
            InstantiateUITransformQuery(World!);
        }

        [Query]
        [None(typeof(UITransformComponent))]
        private void InstantiateUITransform(in Entity entity, CRDTEntity sdkEntity, ref PBUiTransform sdkModel)
        {
            UITransformComponent newTransform = NewUITransformComponent();
            newTransform.Initialize(COMPONENT_NAME, sdkEntity, sdkModel.GetRightOfEntity());
            canvas.rootVisualElement!.Add(newTransform.Transform.EnsureNotNull());
            World!.Add(entity, newTransform);
        }

        private UITransformComponent NewUITransformComponent()
        {
            var component = transformsPool.Get()!;
            //allows to avoid getting from the pool the same element that that already is rootVisualElement
            while (component.Transform == canvas.rootVisualElement) component = transformsPool.Get()!;
            return component;
        }
    }
}
