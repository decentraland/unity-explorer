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

#if !UNITY_WEBGL
        public readonly MultiThreadSync MultiThread;
#endif

        public EmptyScenesWorldSharedDependencies(
                Dictionary<CRDTEntity, Entity> fakeEntitiesMap,
                Entity sceneRoot,
                ISceneData sceneData
#if !UNITY_WEBGL
                ,
                MultiThreadSync multiThreadSync
#endif
                )
        {
            FakeEntitiesMap = fakeEntitiesMap;
            SceneRoot = sceneRoot;
            SceneData = sceneData;
#if !UNITY_WEBGL
            MultiThread = multiThreadSync;
#endif
        }
    }
}
