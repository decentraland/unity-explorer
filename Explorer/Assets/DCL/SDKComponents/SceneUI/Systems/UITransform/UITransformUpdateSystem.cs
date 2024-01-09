using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.Abstract;
using ECS.Groups;
using SceneRunner.Scene;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Systems.UITransform
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(UITransformSortingSystem))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class UITransformUpdateSystem : BaseUnityLoopSystem
    {
        private readonly ISceneStateProvider sceneStateProvider;

        private UITransformUpdateSystem(World world, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
        }

        protected override void Update(float _)
        {
            UpdateUITransformQuery(World);
            CheckUITransformOutOfSceneQuery(World);
        }

        [Query]
        [All(typeof(PBUiTransform), typeof(UITransformComponent))]
        private void UpdateUITransform(ref PBUiTransform sdkModel, ref UITransformComponent uiTransformComponent)
        {
            if (!sdkModel.IsDirty)
                return;

            UiElementUtils.SetupVisualElement(ref uiTransformComponent.Transform, ref sdkModel);
            sdkModel.IsDirty = false;
        }

        [Query]
        [All(typeof(PBUiTransform), typeof(UITransformComponent))]
        private void CheckUITransformOutOfScene(ref UITransformComponent uiTransformComponent)
        {
            if ((sceneStateProvider.IsCurrent && uiTransformComponent.Transform.style.display == DisplayStyle.Flex) ||
                (!sceneStateProvider.IsCurrent && uiTransformComponent.Transform.style.display == DisplayStyle.None))
                return;

            uiTransformComponent.Transform.style.display = !sceneStateProvider.IsCurrent ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
