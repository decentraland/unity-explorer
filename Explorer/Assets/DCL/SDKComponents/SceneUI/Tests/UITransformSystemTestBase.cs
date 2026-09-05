using Arch.Core;
using CRDT;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Systems.UITransform;
using ECS.Abstract;
using ECS.TestSuite;
using NSubstitute;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public abstract class UITransformSystemTestBase<TSystem> : UnitySystemTestBase<TSystem> where TSystem : BaseUnityLoopSystem
    {
        private const string SCENES_UI_ROOT_CANVAS = "ScenesUIRootCanvas";

        protected IComponentPoolsRegistry poolsRegistry;
        protected Entity entity;
        protected UIDocument canvas;
        protected ISceneStateProvider sceneStateProvider;
        protected Dictionary<CRDTEntity, Entity> entitiesMap;

        protected async Task Initialize()
        {
            poolsRegistry = new ComponentPoolsRegistry(
                new Dictionary<Type, IComponentPool>
                {
                    { typeof(UITransformComponent), new ComponentPool.WithDefaultCtor<UITransformComponent>() },
                }, null);

            entity = world.Create();
            canvas = (await Addressables.InstantiateAsync(SCENES_UI_ROOT_CANVAS, trackHandle: false)).GetComponent<UIDocument>();
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            entitiesMap = new Dictionary<CRDTEntity, Entity>();
        }

        protected PBUiTransform CreateUITransform()
        {
            UITransformInstantiationSystem instantiationSystem;
            if (system is UITransformInstantiationSystem transformInstantiationSystem)
                instantiationSystem = transformInstantiationSystem;
            else
                instantiationSystem = new UITransformInstantiationSystem(world, canvas, poolsRegistry);

            var input = new PBUiTransform();
            world.Add(entity, input, new CRDTEntity(20));
            instantiationSystem.Update(0);
            return input;
        }
    }
}
