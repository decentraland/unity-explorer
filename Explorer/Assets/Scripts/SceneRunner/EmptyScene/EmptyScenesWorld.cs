using Arch.Core;
using Arch.SystemGroups;
using CRDT;
using System;
using System.Collections.Generic;
using Utility.Multithreading;

namespace SceneRunner.EmptyScene
{
    public class EmptyScenesWorld : IDisposable
    {
        public readonly SystemGroupWorld SystemGroupWorld;
        public readonly World EcsWorld;
        public readonly MutexSync MutexSync;
        public readonly IDictionary<CRDTEntity, Entity> FakeEntitiesMap;

        public EmptyScenesWorld(SystemGroupWorld systemGroupWorld, IDictionary<CRDTEntity, Entity> fakeEntitiesMap, World ecsWorld, MutexSync mutexSync)
        {
            SystemGroupWorld = systemGroupWorld;
            FakeEntitiesMap = fakeEntitiesMap;
            EcsWorld = ecsWorld;
            MutexSync = mutexSync;
        }

        public void Dispose()
        {
            SystemGroupWorld.Dispose();
            EcsWorld.Dispose();
        }
    }
}
