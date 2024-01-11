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
        protected TSystem system;
        private World cachedWorld;

        protected World world => cachedWorld ??= World.Create();

        [TearDown]
        public void DestroyWorld()
        {
            system?.Dispose();
            cachedWorld?.Dispose();
            cachedWorld = null;
        }

        protected TransformComponent AddTransformToEntity(in Entity entity, bool isDirty = false) =>
            EcsTestsUtils.AddTransformToEntity(world, entity, isDirty);
    }
}
