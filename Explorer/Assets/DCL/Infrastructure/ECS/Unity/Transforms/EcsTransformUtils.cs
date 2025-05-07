using Arch.Core;
using CRDT;
using DCL.Optimization.Pools;
using ECS.Unity.Transforms.Components;
using UnityEngine;
using Utility;

namespace ECS.Unity.Transforms
{
    public static class EcsTransformUtils
    {
        public static TransformComponent CreateTransformComponent(this IComponentPool<Transform> transformPool, Entity entity, CRDTEntity sdkEntity)
        {
            Transform newTransform = transformPool.Get();

            newTransform.SetDebugName(entity, sdkEntity.ToString());
            return new TransformComponent(newTransform);
        }

        /// <summary>
        ///     Assign parent directly without validation
        /// </summary>
        public static void AssignParent(this ref TransformComponent child, EntityReference childEntity, EntityReference parentEntity, in TransformComponent parentComponent)
        {
            child.Transform.SetParent(parentComponent.Transform, true);
            child.Parent = parentEntity;
            parentComponent.Children.Add(childEntity);
        }
    }
}
