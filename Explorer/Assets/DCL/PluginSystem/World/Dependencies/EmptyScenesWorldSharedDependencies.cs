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
        public readonly MultiThreadSync MultiThread;

        public EmptyScenesWorldSharedDependencies(Dictionary<CRDTEntity, Entity> fakeEntitiesMap, Entity sceneRoot,
            ISceneData sceneData, MultiThreadSync multiThreadSync)
        {
            FakeEntitiesMap = fakeEntitiesMap;
            SceneRoot = sceneRoot;
            SceneData = sceneData;
            MultiThread = multiThreadSync;
        }
    }
}
