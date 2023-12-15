using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility.Multithreading;

namespace SceneRunner.EmptyScene
{
    public class EmptySceneFacade : ISceneFacade
    {
        internal static readonly Vector3 GLTF_POSITION = new (8, 0, 8);
        private static readonly IObjectPool<EmptySceneFacade> POOL = new ThreadSafeObjectPool<EmptySceneFacade>(() => new EmptySceneFacade(), defaultCapacity: PoolConstants.EMPTY_SCENES_COUNT);

        private Args args;

        internal Entity sceneRoot { get; private set; } = Entity.Null;

        //internal Entity grass { get; private set; } = Entity.Null;
        internal Entity environment { get; private set; } = Entity.Null;

        private EmptySceneFacade() { }

        public async UniTask DisposeAsync()
        {
            await UniTask.SwitchToThreadPool();

            using (MutexSync.Scope _ = args.MutexSync.GetScope())
            {
                // Remove from map
                args.EntitiesMap.Remove(sceneRoot.Id);

                // Will be cleaned-up by the shared world
                //args.SharedWorld.Add(grass, new DeleteEntityIntention());
                args.SharedWorld.Add(environment, new DeleteEntityIntention());
                args.SharedWorld.Add(sceneRoot, new DeleteEntityIntention());
            }

            POOL.Release(this);

            args = default(Args);
        }

        public UniTask StartUpdateLoopAsync(int targetFPS, CancellationToken ct)
        {
            // Enable creating from the worker thread
            using MutexSync.Scope _ = args.MutexSync.GetScope();

            IComponentPoolsRegistry componentPools = args.ComponentPools;
            EmptySceneMapping mapping = args.Mapping;
            World sharedWorld = args.SharedWorld;

            IComponentPool<SDKTransform> transformPool = componentPools.GetReferenceTypePool<SDKTransform>();
            IComponentPool<PBGltfContainer> gltfContainerPool = componentPools.GetReferenceTypePool<PBGltfContainer>();
            IComponentPool<PartitionComponent> partitionPool = componentPools.GetReferenceTypePool<PartitionComponent>();

            // Create a scene root manually because every scene is running in the shared world
            SDKTransform sceneRootTransform = transformPool.Get();
            sceneRootTransform.Position = args.BasePosition;
            sceneRootTransform.Rotation = Quaternion.identity;
            sceneRootTransform.Scale = Vector3.one;
            sceneRootTransform.ParentId = 0;
            sceneRoot = sharedWorld.Create(sceneRootTransform);

            // Add this root to the map so it can be processed by ParentingTransformSystem
            args.EntitiesMap.Add(sceneRoot.Id, sceneRoot);

            var counter = 0;

            Entity CreateGltf(string file)
            {
                SDKTransform transform = transformPool.Get();
                transform.Rotation = Quaternion.identity;
                transform.Scale = Vector3.one;
                transform.Position = GLTF_POSITION;
                transform.ParentId = sceneRoot.Id;
                PBGltfContainer grassGltf = gltfContainerPool.Get();
                grassGltf.Src = file;
                return sharedWorld.Create(transform, grassGltf, new CRDTEntity(counter++), args.ParentPartition, partitionPool.Get());
            }

            // Logic transferred from JS, otherwise it creates significant overhead
            //grass = CreateGltf(mapping.grass.file); // for some reason grass is already in environment
            environment = CreateGltf(mapping.environment.file);

            return UniTask.CompletedTask;
        }

        public void SetTargetFPS(int fps)
        {
            // has no effect
        }

        UniTask ISceneFacade.StartScene() =>

            // Should be never called as it corresponds to JS logic
            throw new NotImplementedException();

        UniTask ISceneFacade.Tick(float dt) =>
            UniTask.CompletedTask;

        public static EmptySceneFacade Create(Args args)
        {
            EmptySceneFacade f = POOL.Get();
            f.args = args;
            return f;
        }

        public readonly struct Args
        {
            // Map is needed for ParentingTransformSystem
            public readonly IDictionary<CRDTEntity, Entity> EntitiesMap;
            public readonly World SharedWorld;
            public readonly EmptySceneMapping Mapping;
            public readonly IComponentPoolsRegistry ComponentPools;
            public readonly Vector3 BasePosition;
            public readonly IPartitionComponent ParentPartition;
            public readonly MutexSync MutexSync;

            public Args(
                IDictionary<CRDTEntity, Entity> entitiesMap,
                World sharedWorld,
                EmptySceneMapping mapping,
                IComponentPoolsRegistry componentPools,
                Vector3 basePosition,
                IPartitionComponent parentPartition,
                MutexSync mutexSync)
            {
                EntitiesMap = entitiesMap;
                SharedWorld = sharedWorld;
                Mapping = mapping;
                ComponentPools = componentPools;
                BasePosition = basePosition;
                ParentPartition = parentPartition;
                MutexSync = mutexSync;
            }
        }
    }
}
