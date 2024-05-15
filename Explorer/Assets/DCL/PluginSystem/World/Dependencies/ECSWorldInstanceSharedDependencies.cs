using Arch.Core;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Interaction.Utility;
using DCL.Time;
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
        public readonly ISceneStateProvider SceneStateProvider;
        public readonly EntityEventsBuilder EntityEventsBuilder;
        public readonly MutexSync MutexSync;

        public readonly IWorldTimeProvider WorldTimeProvider;

        public ECSWorldInstanceSharedDependencies(
            ISceneData sceneData,
            IPartitionComponent scenePartition,
            IECSToCRDTWriter ecsToCRDTWriter,
            Dictionary<CRDTEntity, Entity> entitiesMap,
            ISceneExceptionsHandler sceneExceptionsHandler,
            IEntityCollidersSceneCache entityCollidersSceneCache,
            ISceneStateProvider sceneStateProvider, EntityEventsBuilder entityEventsBuilder,
            MutexSync mutexSync, IWorldTimeProvider worldTimeProvider)
        {
            SceneData = sceneData;
            EcsToCRDTWriter = ecsToCRDTWriter;
            EntitiesMap = entitiesMap;
            MutexSync = mutexSync;
            ScenePartition = scenePartition;
            SceneStateProvider = sceneStateProvider;
            SceneExceptionsHandler = sceneExceptionsHandler;
            EntityCollidersSceneCache = entityCollidersSceneCache;
            SceneStateProvider = sceneStateProvider;
            WorldTimeProvider = worldTimeProvider;
            EntityEventsBuilder = entityEventsBuilder;
        }
    }
}
