using Arch.Core;
using CrdtEcsBridge.Components.Transform;
using ECS.Abstract;
using ECS.Unity.Transforms.Components;
using NUnit.Framework;
using UnityEngine;

namespace ECS.TestSuite
{
    public abstract class UnitySystemTestBase<TSystem> where TSystem: BaseUnityLoopSystem
    {
        private World cachedWorld;
        protected TSystem system;

        protected World world => cachedWorld ??= World.Create();

        [TearDown]
        public void DestroyWorld()
        {
            system?.Dispose();
            cachedWorld?.Dispose();
            cachedWorld = null;
        }

        /// <summary>
        ///     Adds SDKTransform and creates a new GO with the entity Id as name
        /// </summary>
        protected TransformComponent AddTransformToEntity(in Entity entity, bool isDirty = false)
        {
            var go = new GameObject($"{entity.Id}");
            Transform t = go.transform;

            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            var transformComponent = new TransformComponent(t);

            world.Add(entity, transformComponent, new SDKTransform { IsDirty = isDirty, Position = Vector3.zero, Rotation = Quaternion.identity, Scale = Vector3.one });
            return transformComponent;
        }
    }
}
