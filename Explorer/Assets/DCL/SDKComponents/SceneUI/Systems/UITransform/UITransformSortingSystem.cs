using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using ECS.Abstract;
using ECS.Groups;

namespace DCL.SDKComponents.SceneUI.Systems.UITransform
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(UITransformParentingSystem))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class UITransformSortingSystem : BaseUnityLoopSystem
    {
        internal UITransformSortingSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            // First check if siblings' rightOf has changed
            ResolveSiblingsOrderQuery(World);

            // Change the actual order of VisualElements
            ApplySortingQuery(World);
        }

        [Query]
        private void ResolveSiblingsOrder(in Entity entity, ref PBUiTransform sdkModel, ref UITransformComponent uiTransformComponent)
        {
            if (!sdkModel.IsDirty)
                return;

            // if the entity was added its rightOf will be the same
            // otherwise if it is changed we need to evaluate the new child position

            if (sdkModel.RightOf != uiTransformComponent.RelationData.rightOf)
            {
                // Require parent to re-evaluate its children order

                if (uiTransformComponent.RelationData.parent != EntityReference.Null)
                {
                    ref var parent = ref World.Get<UITransformComponent>(uiTransformComponent.RelationData.parent);
                    parent.RelationData.ChangeChildRightOf(uiTransformComponent.RelationData.rightOf, sdkModel.RightOf, World.Reference(entity));
                }

                uiTransformComponent.RelationData.rightOf = sdkModel.RightOf;
            }
        }

        [Query]
        private void ApplySorting(ref UITransformComponent uiTransformComponent)
        {
            uiTransformComponent.SortIfRequired(World);
        }
    }
}
