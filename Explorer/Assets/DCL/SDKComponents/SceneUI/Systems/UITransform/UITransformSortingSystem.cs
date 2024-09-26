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
using UnityEngine.Assertions;

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
        private void ResolveSiblingsOrder(ref PBUiTransform sdkModel, ref UITransformComponent uiTransformComponent)
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

                        if (newRightOfComponent.RelationData.parent != EntityReference.Null)
                        {
                            Assert.AreEqual(uiTransformComponent.RelationData.parent, newRightOfComponent.RelationData.parent);
                            parent.RelationData.ChangeChildRightOf(uiTransformComponent.RelationData.rightOf, newRightOf, ref newRightOfComponent.RelationData);
                        }
                        else if (!newRightOfComponent.IsRoot)
                            ReportHub.LogError(ReportCategory.SCENE_UI, $"Can't Resolve sibling order for entity: {uiTransformComponent.RelationData.parent.Entity.ToString()} - as its new RightOfEntity: {newRightOfEntity.ToString()} - has no parent, but it is NOT a ROOT either");
                    }
                    else
                    {
                        // TODO fail-safe, make unsorted?
                    }
                }

                uiTransformComponent.RelationData.rightOf = newRightOf;
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
