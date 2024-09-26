using Arch.Core;
using CrdtEcsBridge.Components.Transform;
using ECS.Unity.Transforms.Components;
using UnityEngine;
using Utility;

namespace ECS.TestSuite
{
    public static class EcsTestsUtils
    {
        /// <summary>
        ///     Adds SDKTransform and creates a new GO with the entity Id as name
        /// </summary>
        public static TransformComponent AddTransformToEntity(World world, in Entity entity, bool isDirty = false)
        {
            var go = new GameObject($"{entity.Id}");
            Transform t = go.transform;

            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            var transformComponent = new TransformComponent(t);

            world.Add(entity, transformComponent, new SDKTransform { IsDirty = isDirty, Position = new CanBeDirty<Vector3>(Vector3.zero), Rotation = new CanBeDirty<Quaternion>(Quaternion.identity), Scale = Vector3.one });
            return transformComponent;
        }
    }
}
