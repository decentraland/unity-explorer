using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Systems.UITransform;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Systems.UIPointerEvents
{
    /// <summary>
    ///     Keeps the UI Toolkit pointer callbacks of a UI element in sync with its <see cref="PBPointerEvents" />.
    ///     The callbacks queue events on <see cref="UITransformComponent.PendingPointerEvents" />; the queue is written to the
    ///     scene every frame by <c>WritePointerEventResultsSystem</c>, together with the world and global pointer results,
    ///     so a UI event never gets a later tick than the global event of the same press.
    /// </summary>
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(UITransformUpdateSystem))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class UIPointerEventsSystem : BaseUnityLoopSystem
    {
        public UIPointerEventsSystem(World world) : base(world) { }

        protected override void Update(float _)
        {
            RegisterPointerCallbacksQuery(World);

            HandleEntityDestructionQuery(World);
            HandleUiPointerEventsRemovalQuery(World);
        }

        [Query]
        private void RegisterPointerCallbacks(ref PBPointerEvents sdkModel, ref UITransformComponent uiTransformComponent)
        {
            if (!sdkModel.IsDirty)
                return;

            uiTransformComponent.RegisterPointerCallbacks();
            sdkModel.IsDirty = false;
        }

        [Query]
        [None(typeof(PBPointerEvents), typeof(DeleteEntityIntention))]
        private void HandleUiPointerEventsRemoval(ref UITransformComponent uiTransformComponent, in PBUiTransform sdkModel) =>
            RemovePointerEvents(ref uiTransformComponent, in sdkModel);

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref UITransformComponent uiTransformComponent, in PBUiTransform sdkModel) =>
            RemovePointerEvents(ref uiTransformComponent, in sdkModel);

        private static void RemovePointerEvents(ref UITransformComponent uiTransformComponent, in PBUiTransform sdkModel)
        {
            uiTransformComponent.Transform.pickingMode = sdkModel.PointerFilter == PointerFilterMode.PfmBlock ? PickingMode.Position : PickingMode.Ignore;
            uiTransformComponent.UnregisterPointerCallbacks();
            uiTransformComponent.Transform.UnregisterHoverStyleCallbacks();
            uiTransformComponent.PendingPointerEvents.Clear();
        }
    }
}
