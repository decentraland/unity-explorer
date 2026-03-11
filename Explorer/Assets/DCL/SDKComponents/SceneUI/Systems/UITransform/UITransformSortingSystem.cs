using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CRDT;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Defaults;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using System.Collections.Generic;

namespace DCL.SDKComponents.SceneUI.Systems.UITransform
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(UITransformParentingSystem))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class UITransformSortingSystem : BaseUnityLoopSystem
    {
        private readonly IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap;

        internal UITransformSortingSystem(World world, IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap) : base(world)
        {
            this.entitiesMap = entitiesMap;
        }

        protected override void Update(float t)
        {
            // First check if siblings' rightOf has changed
            ResolveSiblingsOrderQuery(World);

            // Change the actual order of VisualElements
            ApplySortingQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ResolveSiblingsOrder(CRDTEntity sdkEntity, ref PBUiTransform sdkModel, ref UITransformComponent uiTransformComponent)
        {
            if (!sdkModel.IsDirty)
                return;

            var newRightOf = sdkModel.GetRightOfEntity();

            if (!newRightOf.Equals(uiTransformComponent.RelationData.rightOf))
            {
                Entity parentEntity = uiTransformComponent.RelationData.parent;

                if (parentEntity != Entity.Null)
                {
                    ref var parent = ref World.Get<UITransformComponent>(parentEntity);

                    // Remove this child from its current position in the parent's linked list
                    parent.RelationData.RemoveChild(sdkEntity, ref uiTransformComponent.RelationData);

                    // Update rightOf to the new value so AddChild positions correctly
                    uiTransformComponent.RelationData.rightOf = newRightOf;

                    // Re-add this child at the new position determined by the updated rightOf
                    parent.RelationData.AddChild(parentEntity, sdkEntity, ref uiTransformComponent.RelationData);
                }
                else
                {
                    uiTransformComponent.RelationData.rightOf = newRightOf;
                }
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ApplySorting(ref UITransformComponent uiTransformComponent)
        {
            uiTransformComponent.SortIfRequired(World, entitiesMap);
        }
    }
}
