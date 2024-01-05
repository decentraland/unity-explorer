using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using ECS.Abstract;
using ECS.Groups;
using DCL.SDKComponents.SceneUI.Utils;

namespace DCL.SDKComponents.SceneUI.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(UITransformSortingSystem))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class UITransformUpdateSystem : BaseUnityLoopSystem
    {
        public UITransformUpdateSystem(World world) : base(world) { }

        protected override void Update(float _)
        {
            UpdateUITransformQuery(World);
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
    }
}
