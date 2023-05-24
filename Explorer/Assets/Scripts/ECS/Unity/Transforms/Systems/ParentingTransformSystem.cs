using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.Unity.Transforms.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InstantiateTransformSystem))]
    public partial class ParentingTransformSystem : BaseUnityLoopSystem
    {
        private readonly EntityReference sceneRootEntityReference;
        private readonly IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap;

        public ParentingTransformSystem(World world, IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap, EntityReference sceneRootEntityReference) : base(world)
        {
            this.sceneRootEntityReference = sceneRootEntityReference;
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
                SetNewChild(ref World.Get<TransformComponent>(childEntity.Entity),
                    childEntity, sceneRootEntityReference);
            }
            transformComponentToBeDeleted.Children.Clear();
        }

        [Query]
        [All(typeof(SDKTransform), typeof(TransformComponent))]
        private void DoParenting(in Entity entity, ref SDKTransform sdkTransform, ref TransformComponent transformComponent)
        {
            if (!sdkTransform.IsDirty) return;

            EntityReference parentReference = sceneRootEntityReference;

            if (entitiesMap.TryGetValue(sdkTransform.ParentId, out Entity newParentEntity))
            {
                parentReference = World.Reference(newParentEntity);

                //We have to remove the child from the old parent
                if (transformComponent.Parent != parentReference)
                    RemoveFromParent(transformComponent, World.Reference(entity));
            }

            SetNewChild(ref transformComponent, World.Reference(entity), parentReference);
        }

        private void SetNewChild(ref TransformComponent childComponent, EntityReference childEntityReference,
            EntityReference parentEntityReference)
        {
            if (childComponent.Parent == parentEntityReference)
                return;

            if (!parentEntityReference.IsAlive(World))
            {
                Debug.LogError($"Trying to parent entity {childEntityReference.Entity} to a dead entity parent");
                return;
            }

            TransformComponent parentComponent = World.Get<TransformComponent>(parentEntityReference.Entity);
            childComponent.Transform.SetParent(parentComponent.Transform, true);
            childComponent.Parent = parentEntityReference;
            parentComponent.Children.Add(childEntityReference);
        }

        private void RemoveFromParent(TransformComponent childComponent, EntityReference childEntityReference)
        {
            if (childComponent.Parent.IsAlive(World))
                World.Get<TransformComponent>(childComponent.Parent.Entity).Children.Remove(childEntityReference);
        }
    }
}
