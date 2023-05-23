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
        private readonly TransformComponent sceneRootTransformComponent;
        private readonly IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap;

        public ParentingTransformSystem(World world, IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap, TransformComponent sceneRootTransformComponent) : base(world)
        {
            this.sceneRootTransformComponent = sceneRootTransformComponent;
            this.entitiesMap = entitiesMap;
        }

        protected override void Update(float t)
        {
            OrphanChildsOfDeletedEntityQuery(World);
            DoParentingQuery(World);
        }

        [Query]
        [All(typeof(SDKTransform), typeof(TransformComponent), typeof(DeleteEntityIntention))]
        private void OrphanChildsOfDeletedEntity(ref TransformComponent transformComponentToBeDeleted)
        {
            foreach (TransformComponent childEntity in transformComponentToBeDeleted.Children)
                SetNewChild(sceneRootTransformComponent, childEntity);

            transformComponentToBeDeleted.Children.Clear();
        }

        [Query]
        [All(typeof(SDKTransform), typeof(TransformComponent))]
        private void DoParenting(in Entity entity, ref SDKTransform sdkTransform, ref TransformComponent transformComponent)
        {
            if (!sdkTransform.IsDirty) return;

            TransformComponent newParent = sceneRootTransformComponent;

            if (entitiesMap.TryGetValue(sdkTransform.ParentId, out Entity newParentEntity))
            {
                newParent = World.Get<TransformComponent>(newParentEntity);

                //We have to remove the child from the old parent
                if (transformComponent.Parent != newParent)
                    transformComponent.Parent?.Children.Remove(transformComponent);
            }

            SetNewChild(newParent, transformComponent);
        }

        private void SetNewChild(TransformComponent parentComponent, TransformComponent childComponent)
        {
            if (childComponent.Parent == parentComponent)
                return;

            childComponent.Transform.SetParent(parentComponent.Transform, true);
            childComponent.Parent = parentComponent;
            parentComponent.Children.Add(childComponent);
        }
    }
}
