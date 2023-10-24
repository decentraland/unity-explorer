using Arch.Core;
using CrdtEcsBridge.Components.Transform;
using ECS.Abstract;
using ECS.Profiling;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using ECS.Unity.Transforms.Components;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.TestSuite
{
    public abstract class UnitySystemTestBase<TSystem> where TSystem: BaseUnityLoopSystem
    {
        protected readonly MemoryBudgetProvider memoryBudgetProvider = new (new Dictionary<Type, int>(), Substitute.For<IProfilingProvider>());
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
