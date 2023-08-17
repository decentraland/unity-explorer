using Arch.Core;
using CRDT;
using SceneRunner.Scene;
using System.Collections.Generic;
using Utility.Multithreading;

namespace DCL.PluginSystem.World.Dependencies
{
    public readonly struct EmptyScenesWorldSharedDependencies
    {
        public readonly Dictionary<CRDTEntity, Entity> FakeEntitiesMap;
        public readonly Entity SceneRoot;
        public readonly ISceneData SceneData;
        public readonly MutexSync Mutex;

        public EmptyScenesWorldSharedDependencies(Dictionary<CRDTEntity, Entity> fakeEntitiesMap, Entity sceneRoot, ISceneData sceneData, MutexSync mutex)
        {
            FakeEntitiesMap = fakeEntitiesMap;
            SceneRoot = sceneRoot;
            SceneData = sceneData;
            Mutex = mutex;
        }
    }
}
