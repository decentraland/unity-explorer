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
            foreach (TransformComponent childEntity in transformComponentToBeDeleted.Children)
            {
                TransformComponent transformComponent = childEntity;
                SetNewChild(ref transformComponent, sceneRootEntityReference);
            }

            transformComponentToBeDeleted.Children.Clear();
        }

        [Query]
        [All(typeof(SDKTransform), typeof(TransformComponent))]
        private void DoParenting(ref SDKTransform sdkTransform, ref TransformComponent transformComponent)
        {
            if (!sdkTransform.IsDirty) return;

            EntityReference parentReference = sceneRootEntityReference;

            if (entitiesMap.TryGetValue(sdkTransform.ParentId, out Entity newParentEntity))
            {
                parentReference = World.Reference(newParentEntity);

                //We have to remove the child from the old parent
                if (transformComponent.Parent != parentReference)
                    RemoveFromParent(transformComponent);
            }

            SetNewChild(ref transformComponent, parentReference);
        }

        private void SetNewChild(ref TransformComponent childComponent, EntityReference parentEntityReference)
        {
            if (childComponent.Parent == parentEntityReference)
                return;

            TransformComponent parentComponent = World.Get<TransformComponent>(parentEntityReference.Entity);

            childComponent.Transform.SetParent(parentComponent.Transform, true);
            childComponent.Parent = parentEntityReference;
            parentComponent.Children.Add(childComponent);
        }

        private void RemoveFromParent(TransformComponent childToRemove)
        {
            if (childToRemove.Parent != EntityReference.Null)
                World.Get<TransformComponent>(childToRemove.Parent.Entity).Children.Remove(childToRemove);
        }
    }
}
