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
        private void ResolveSiblingsOrder(in Entity entity, ref PBUiTransform sdkModel, ref UITransformComponent uiTransformComponent)
        {
            if (!sdkModel.IsDirty)
                return;

            // if the entity was added its rightOf will be the same
            // otherwise if it is changed we need to evaluate the new child position

            var newRightOf = sdkModel.GetRightOfEntity();

            if (!newRightOf.Equals(uiTransformComponent.RelationData.rightOf))
            {
                // Require parent to re-evaluate its children order

                if (uiTransformComponent.RelationData.parent != EntityReference.Null)
                {
                    ref var parent = ref World.Get<UITransformComponent>(uiTransformComponent.RelationData.parent);

                    if (entitiesMap.TryGetValue(newRightOf, out var newRightOfEntity))
                    {
                        ref var newRightOfComponent = ref World.Get<UITransformComponent>(newRightOfEntity);

                        parent.RelationData.ChangeChildRightOf(uiTransformComponent.RelationData.rightOf,
                            newRightOf,
                            ref newRightOfComponent.RelationData);
                    }
                    else
                    {
                        // TODO fail-safe, make unsorted?
                    }
                }

                uiTransformComponent.RelationData.rightOf = sdkModel.GetRightOfEntity();
            }
        }

        [Query]
        private void ApplySorting(ref UITransformComponent uiTransformComponent)
        {
            uiTransformComponent.SortIfRequired(World, entitiesMap);
        }
    }
}
