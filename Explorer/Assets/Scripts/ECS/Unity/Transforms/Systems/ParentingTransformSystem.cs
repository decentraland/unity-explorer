using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CRDT;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Transform;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.Unity.Transforms.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(InstantiateTransformSystem))]
    [ThrottlingEnabled]
    public partial class ParentingTransformSystem : BaseUnityLoopSystem
    {
        private readonly Entity sceneRoot;
        private readonly IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap;
        private readonly SceneShortInfo sceneShortInfo;

        public ParentingTransformSystem(World world, IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap, Entity sceneRoot, SceneShortInfo sceneShortInfo) : base(world)
        {
            this.sceneRoot = sceneRoot;
            this.sceneShortInfo = sceneShortInfo;
            this.entitiesMap = entitiesMap;
        }

        protected override void Update(float t)
        {
            OrphanChildrenOfDeletedEntityQuery(World);
            DoParentingQuery(World);
        }

        [Query]
        [All(typeof(SDKTransform), typeof(TransformComponent), typeof(DeleteEntityIntention))]
        private void OrphanChildrenOfDeletedEntity(ref TransformComponent transformComponentToBeDeleted)
        {
            foreach (EntityReference childEntity in transformComponentToBeDeleted.Children)
            {
                ref var transformComponent = ref World.Get<TransformComponent>(childEntity.Entity);

                SetNewChild(
                    ref transformComponent,
                    childEntity,
                    0,
                    sceneRoot,
                    SpecialEntitiesID.SCENE_ROOT_ENTITY
                );

                transformComponent.SetTransform(Vector3.zero, Quaternion.identity, Vector3.one);
            }

            transformComponentToBeDeleted.Children.Clear();
        }

        [Query]
        [All(typeof(SDKTransform), typeof(TransformComponent))]
        private void DoParenting(in Entity entity, CRDTEntity crdtEntity, ref SDKTransform sdkTransform, ref TransformComponent transformComponent)
        {
            if (!sdkTransform.IsDirty) return;

            Entity parentReference = sceneRoot;

            if (entitiesMap.TryGetValue(sdkTransform.ParentId, out Entity newParentEntity))
            {
                parentReference = newParentEntity;

                //We have to remove the child from the old parent
                if (transformComponent.Parent != parentReference)
                    RemoveFromParent(transformComponent, World.Reference(entity));
            }

            SetNewChild(ref transformComponent, World.Reference(entity), crdtEntity, parentReference, sdkTransform.ParentId);
        }

        private void SetNewChild(ref TransformComponent childComponent, EntityReference childEntityReference, CRDTEntity childCRDTEntity,
            Entity parentEntity, CRDTEntity parentId)
        {
            if (childComponent.Parent == parentEntity)
                return;

            if (!World.IsAlive(parentEntity))
            {
                ReportHub.LogError(new ReportData(GetReportCategory(), sceneShortInfo: sceneShortInfo), $"Trying to parent entity {childEntityReference.Entity} ({childCRDTEntity}) to a dead entity parent");
                return;
            }

            ref TransformComponent parentComponent = ref World.TryGetRef<TransformComponent>(parentEntity, out bool success);

            if (!success)
            {
                ReportHub.LogError(new ReportData(GetReportCategory(), sceneShortInfo: sceneShortInfo), $"Trying to parent entity {childEntityReference.Entity} ({childCRDTEntity}) to parent {parentEntity} ({parentId}) that doesn't have a TransformComponent");
                return;
            }

            childComponent.AssignParent(childEntityReference, World.Reference(parentEntity), in parentComponent);
        }

        private void RemoveFromParent(TransformComponent childComponent, EntityReference childEntityReference)
        {
            if (childComponent.Parent.IsAlive(World))
                World.Get<TransformComponent>(childComponent.Parent.Entity).Children.Remove(childEntityReference);
        }
    }
}
