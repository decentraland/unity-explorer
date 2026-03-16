using Arch.Core;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using CrdtEcsBridge.UpdateGate;
using DCL.Interaction.Utility;
using ECS.Abstract;
using ECS.Prioritization.Components;
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using System.Collections.Generic;
using Utility.Multithreading;

namespace DCL.PluginSystem.World.Dependencies
{
    public readonly struct ECSWorldInstanceSharedDependencies
    {
        public readonly ISceneData SceneData;
        public readonly IPartitionComponent ScenePartition;
        public readonly IECSToCRDTWriter EcsToCRDTWriter;
        public readonly Dictionary<CRDTEntity, Entity> EntitiesMap;
        public readonly ISceneExceptionsHandler SceneExceptionsHandler;
        public readonly IEntityCollidersSceneCache EntityCollidersSceneCache;
        public readonly IEntityCollidersGlobalCache EntityCollidersGlobalCache;
        public readonly ISceneStateProvider SceneStateProvider;
        public readonly EntityEventsBuilder EntityEventsBuilder;
        public readonly ISystemGroupsUpdateGate EcsGroupThrottler;
        public readonly ISystemsUpdateGate EcsSystemsGate;
#if !UNITY_WEBGL
        public readonly MultiThreadSync MultiThreadSync;
#endif

        public ECSWorldInstanceSharedDependencies(
            ISceneData sceneData,
            IPartitionComponent scenePartition,
            IECSToCRDTWriter ecsToCRDTWriter,
            Dictionary<CRDTEntity, Entity> entitiesMap,
            ISceneExceptionsHandler sceneExceptionsHandler,
            IEntityCollidersSceneCache entityCollidersSceneCache,
            IEntityCollidersGlobalCache entityCollidersGlobalCache,
            ISceneStateProvider sceneStateProvider,
            EntityEventsBuilder entityEventsBuilder,
#if !UNITY_WEBGL
            MultiThreadSync multiThreadSync,
#endif
            ISystemGroupsUpdateGate ecsGroupThrottler,
            ISystemsUpdateGate ecsSystemsGate
            )
        {
            SceneData = sceneData;
            EcsToCRDTWriter = ecsToCRDTWriter;
            EntitiesMap = entitiesMap;
#if !UNITY_WEBGL
            MultiThreadSync = multiThreadSync;
#endif
            ScenePartition = scenePartition;
            SceneStateProvider = sceneStateProvider;
            SceneExceptionsHandler = sceneExceptionsHandler;
            EntityCollidersSceneCache = entityCollidersSceneCache;
            EntityCollidersGlobalCache = entityCollidersGlobalCache;
            EcsGroupThrottler = ecsGroupThrottler;
            EcsSystemsGate = ecsSystemsGate;
            EntityEventsBuilder = entityEventsBuilder;
        }
    }
}
