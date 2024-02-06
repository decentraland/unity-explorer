using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Utils;
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
        private const string COMPONENT_NAME = "UITransform";

        private readonly UIDocument canvas;
        private readonly IComponentPool<UITransformComponent> transformsPool;

        public UITransformInstantiationSystem(World world, UIDocument canvas, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            this.canvas = canvas;
            transformsPool = poolsRegistry.GetReferenceTypePool<UITransformComponent>();
        }

        protected override void Update(float t)
        {
            InstantiateUITransformQuery(World);
        }

        [Query]
        [None(typeof(UITransformComponent))]
        private void InstantiateUITransform(in Entity entity, ref PBUiTransform sdkModel)
        {
            UITransformComponent newTransform = transformsPool.Get();
            newTransform.Transform ??= new VisualElement();
            newTransform.Transform.name = UiElementUtils.BuildElementName(COMPONENT_NAME, entity);
            newTransform.Parent = EntityReference.Null;
            newTransform.Children = HashSetPool<EntityReference>.Get();
            newTransform.IsHidden = false;
            newTransform.RightOf = sdkModel.RightOf;
            newTransform.UnregisterAllCallbacks();
            canvas.rootVisualElement.Add(newTransform.Transform);
            World.Add(entity, newTransform);
        }
    }
}
