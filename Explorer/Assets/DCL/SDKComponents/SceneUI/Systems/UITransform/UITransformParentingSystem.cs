﻿using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CRDT;
using CrdtEcsBridge.Components.Special;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using System.Collections.Generic;

namespace DCL.SDKComponents.SceneUI.Systems.UITransform
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(UITransformInstantiationSystem))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class UITransformParentingSystem : BaseUnityLoopSystem
    {
        private readonly Entity sceneRoot;
        private readonly IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap;

        internal UITransformParentingSystem(World world, IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap, Entity sceneRoot) : base(world)
        {
            this.sceneRoot = sceneRoot;
            this.entitiesMap = entitiesMap;
        }

        protected override void Update(float t)
        {
            OrphanChildrenOfDeletedEntityQuery(World);
            DoUITransformParentingQuery(World);
        }

        [Query]
        [All(typeof(PBUiTransform), typeof(DeleteEntityIntention))]
        [None(typeof(SceneRootComponent))]
        private void OrphanChildrenOfDeletedEntity(CRDTEntity sdkEntity, ref UITransformComponent uiTransformComponentToBeDeleted)
        {
            // Remove deleted entity from the parent list
            RemoveFromParent(uiTransformComponentToBeDeleted, sdkEntity);

            var head = uiTransformComponentToBeDeleted.RelationData.head;
            if (head == null) return;

            for (var current = head; current != null; current = current.Next)
            {
                if (entitiesMap.TryGetValue(current.EntityId, out Entity childEntity))
                {
                    ref UITransformComponent uiTransform = ref World.TryGetRef<UITransformComponent>(childEntity, out bool exists);

                    if (!exists)
                    {
                        ReportHub.LogError(GetReportData(), $"Trying to unparent an ${nameof(UITransformComponent)}'s child but no component has been found on entity {current.EntityId}");
                        continue;
                    }

                    uiTransformComponentToBeDeleted.RelationData.RemoveChild(current.EntityId, ref uiTransform.RelationData);
                    SetNewChild(ref uiTransform, current.EntityId, sceneRoot);
                }
            }
        }

        [Query]
        [None(typeof(SceneRootComponent), typeof(DeleteEntityIntention))]
        private void DoUITransformParenting(CRDTEntity sdkEntity, ref PBUiTransform sdkModel, ref UITransformComponent uiTransformComponent)
        {
            if (!sdkModel.IsDirty)
                return;

            if (entitiesMap.TryGetValue(sdkModel.Parent, out Entity newParentEntity))
            {
                Entity parentReference = newParentEntity;

                //We have to remove the child from the old parent
                if (uiTransformComponent.RelationData.parent != parentReference)
                    RemoveFromParent(uiTransformComponent, sdkEntity);

                SetNewChild(ref uiTransformComponent, sdkEntity, parentReference);
            }
        }

        private void SetNewChild(ref UITransformComponent childComponent, CRDTEntity childEntity, Entity parentEntity)
        {
            if (childComponent.RelationData.parent == parentEntity)
                return;

            if (!World.IsAlive(parentEntity))
            {
                ReportHub.LogError(GetReportData(), $"Trying to parent entity {childEntity} to a dead entity parent");
                return;
            }

            ref UITransformComponent parentComponent = ref World.TryGetRef<UITransformComponent>(parentEntity, out bool exists);

            if (!exists)
            {
                ReportHub.LogError(GetReportData(), $"Trying to parent entity {childEntity} to a parent {parentEntity} that do not have ${nameof(UITransformComponent)} component");
                return;
            }

            if (parentComponent == childComponent) return;

            parentComponent.RelationData.AddChild(parentEntity, childEntity, ref childComponent.RelationData);
            parentComponent.Transform.Add(childComponent.Transform);
        }

        private void RemoveFromParent(UITransformComponent childComponent, CRDTEntity child)
        {
            if (!World.IsAlive(childComponent.RelationData.parent)) return;

            ref UITransformComponent parentTransform = ref World.TryGetRef<UITransformComponent>(childComponent.RelationData.parent, out bool exists);

            if (!exists)
            {
                ReportHub.LogError(GetReportData(), $"Trying to remove a child from a parent {childComponent.RelationData.parent} that do not have ${nameof(UITransformComponent)} component");
                return;
            }

            parentTransform.RelationData.RemoveChild(child, ref childComponent.RelationData);
        }
    }
}
