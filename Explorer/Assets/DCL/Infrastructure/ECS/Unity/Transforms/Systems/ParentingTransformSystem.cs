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
using System.Diagnostics;
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
        [All(typeof(DeleteEntityIntention))]
        private void DereferenceParentingOfDeletedEntity(in Entity entity, ref TransformComponent transformComponentToBeDeleted)
        {
            RemoveFromParent(in transformComponentToBeDeleted, entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void OrphanChildrenOfDeletedEntity(in Entity entity, in CRDTEntity crdtEntity, ref TransformComponent transformComponentToBeDeleted)
        {
            foreach (Entity childEntity in transformComponentToBeDeleted.Children)
            {
                // Arch ignores the entity version, so a stale id can resolve to a dead slot or to an unrelated recycled entity
                if (!World.IsAlive(childEntity))
                {
                    ReportStaleChild(entity, crdtEntity, childEntity, false, false, Entity.Null);
                    continue;
                }

                ref TransformComponent transformComponent = ref World.TryGetRef<TransformComponent>(childEntity, out bool hasTransform);

                if (!hasTransform || transformComponent.Parent != entity)
                {
                    ReportStaleChild(entity, crdtEntity, childEntity, true, hasTransform, hasTransform ? transformComponent.Parent : Entity.Null);
                    continue;
                }

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

            // A parent the scene never materialized in this world falls back to the scene root
            if (!entitiesMap.TryGetValue(sdkTransform.ParentId, out Entity parentReference))
                parentReference = sceneRoot;

            if (transformComponent.Parent == parentReference) return;

            RemoveFromParent(in transformComponent, entity);
            SetNewChild(ref transformComponent, entity, crdtEntity, parentReference, sdkTransform.ParentId);
        }

        private void SetNewChild(ref TransformComponent childComponent, Entity childEntityReference, CRDTEntity childCRDTEntity,
            Entity parentEntity, CRDTEntity parentId)
        {
            if (childComponent.Parent == parentEntity)
                return;

            if (!World.IsAlive(parentEntity))
            {
                ReportHub.LogError(GetReportData(), $"Trying to parent entity {childEntityReference} ({childCRDTEntity}) to a dead entity parent, falling back to the scene root");
                AssignToSceneRoot(ref childComponent, childEntityReference);
                return;
            }

            ref TransformComponent parentComponent = ref World.TryGetRef<TransformComponent>(parentEntity, out bool success);

            if (!success)
            {
                ReportHub.LogError(GetReportData(), $"Trying to parent entity {childEntityReference} ({childCRDTEntity}) to parent {parentEntity} ({parentId}) that doesn't have a TransformComponent, falling back to the scene root");
                AssignToSceneRoot(ref childComponent, childEntityReference);
                return;
            }

            childComponent.AssignParent(childEntityReference, parentEntity, in parentComponent);
        }

        // Keeps Parent and the parent's Children consistent when the requested parent cannot be used
        private void AssignToSceneRoot(ref TransformComponent childComponent, Entity childEntityReference)
        {
            if (childComponent.Parent == sceneRoot)
                return;

            childComponent.AssignParent(childEntityReference, sceneRoot, in World.Get<TransformComponent>(sceneRoot));
        }

        private void RemoveFromParent(in TransformComponent childComponent, Entity childEntityReference)
        {
            if (!World.IsAlive(childComponent.Parent)) return;

            ref TransformComponent parentComponent = ref World.TryGetRef<TransformComponent>(childComponent.Parent, out bool exists);

            if (exists && !parentComponent.Children.Remove(childEntityReference))
                ReportHub.LogError(GetReportData(), $"Entity {childEntityReference} is not a child of its parent {childComponent.Parent}");
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        private void ReportStaleChild(Entity deletedEntity, CRDTEntity deletedCrdtEntity, Entity childEntity, bool childIsAlive, bool childHasTransform, Entity childParent)
        {
            ReportHub.LogWarning(GetReportData(),
                $"Deleted entity {deletedEntity} ({deletedCrdtEntity}) lists {childEntity} as a child but it is not one anymore: alive={childIsAlive}, hasTransform={childHasTransform}, parent={childParent}");
        }
    }
}
