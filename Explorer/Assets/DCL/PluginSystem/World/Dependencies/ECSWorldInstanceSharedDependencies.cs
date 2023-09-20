using Arch.Core;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Interaction.Utility;
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using System.Collections.Generic;
using Utility.Multithreading;

namespace DCL.PluginSystem.World.Dependencies
{
    public readonly struct ECSWorldInstanceSharedDependencies
    {
        public readonly ISceneData SceneData;
        public readonly IECSToCRDTWriter EcsToCRDTWriter;
        public readonly IReadOnlyDictionary<CRDTEntity, Entity> EntitiesMap;
        public readonly ISceneExceptionsHandler SceneExceptionsHandler;
        public readonly IEntityCollidersSceneCache EntityCollidersSceneCache;
        public readonly ISceneStateProvider SceneStateProvider;
        public readonly MutexSync MutexSync;

        public ECSWorldInstanceSharedDependencies(
            ISceneData sceneData,
            IECSToCRDTWriter ecsToCRDTWriter,
            IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap,
            ISceneExceptionsHandler sceneExceptionsHandler,
            IEntityCollidersSceneCache entityCollidersSceneCache,
            ISceneStateProvider sceneStateProvider,
            MutexSync mutexSync)
        {
            SceneData = sceneData;
            EcsToCRDTWriter = ecsToCRDTWriter;
            EntitiesMap = entitiesMap;
            MutexSync = mutexSync;
            SceneStateProvider = sceneStateProvider;
            SceneExceptionsHandler = sceneExceptionsHandler;
            EntityCollidersSceneCache = entityCollidersSceneCache;
            SceneStateProvider = sceneStateProvider;
        }
    }
}
