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

        public ParentingTransformSystem(World world, IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap, Entity sceneRoot) : base(world)
        {
            this.sceneRoot = sceneRoot;
            this.entitiesMap = entitiesMap;
        }

        protected override void Update(float t)
        {
            OrphanChildrenOfDeletedEntityQuery(World);
            DereferenceParentingOfDeletedEntityQuery(World);
            DoParentingQuery(World);
        }

        [Query]
        [All(typeof(SDKTransform), typeof(DeleteEntityIntention))]
        private void DereferenceParentingOfDeletedEntity(in Entity entity, ref TransformComponent transformComponentToBeDeleted)
        {
            var parentTransform = World!.TryGetRef<TransformComponent>(transformComponentToBeDeleted.Parent, out bool exists);

            if (exists && parentTransform.Children.Remove(entity) == false)
                ReportHub.LogError(
                    GetReportData(),
                    $"Entity {entity} is not a child of its parent {transformComponentToBeDeleted.Parent}"
                );
        }

        [Query]
        [All(typeof(SDKTransform), typeof(DeleteEntityIntention))]
        private void OrphanChildrenOfDeletedEntity(ref TransformComponent transformComponentToBeDeleted)
        {
            foreach (Entity childEntity in transformComponentToBeDeleted.Children)
            {
                ref TransformComponent transformComponent = ref World!.Get<TransformComponent>(childEntity);

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
                    RemoveFromParent(transformComponent, entity);
            }

            SetNewChild(ref transformComponent, entity, crdtEntity, parentReference, sdkTransform.ParentId);
        }

        private void SetNewChild(ref TransformComponent childComponent, Entity childEntityReference, CRDTEntity childCRDTEntity,
            Entity parentEntity, CRDTEntity parentId)
        {
            if (childComponent.Parent == parentEntity)
                return;

            if (!World.IsAlive(parentEntity))
            {
                ReportHub.LogError(GetReportData(), $"Trying to parent entity {childEntityReference} ({childCRDTEntity}) to a dead entity parent");
                return;
            }

            ref TransformComponent parentComponent = ref World.TryGetRef<TransformComponent>(parentEntity, out bool success);

            if (!success)
            {
                ReportHub.LogError(GetReportData(), $"Trying to parent entity {childEntityReference} ({childCRDTEntity}) to parent {parentEntity} ({parentId}) that doesn't have a TransformComponent");
                return;
            }

            childComponent.AssignParent(childEntityReference, parentEntity, in parentComponent);
        }

        private void RemoveFromParent(TransformComponent childComponent, Entity childEntityReference)
        {
            if (World.IsAlive(childComponent.Parent))
                World.Get<TransformComponent>(childComponent.Parent).Children.Remove(childEntityReference);
        }
    }
}
