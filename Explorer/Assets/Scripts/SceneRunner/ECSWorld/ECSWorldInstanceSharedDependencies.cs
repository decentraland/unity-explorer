using Arch.Core;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using SceneRunner.Scene;
using System.Collections.Generic;

namespace SceneRunner.ECSWorld
{
    public readonly struct ECSWorldInstanceSharedDependencies
    {
        public readonly ISceneData SceneData;
        public readonly IECSToCRDTWriter EcsToCRDTWriter;
        public readonly IReadOnlyDictionary<CRDTEntity, Entity> EntitiesMap;

        public ECSWorldInstanceSharedDependencies(
            ISceneData sceneData,
            IECSToCRDTWriter ecsToCRDTWriter,
            IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap)
        {
            SceneData = sceneData;
            EcsToCRDTWriter = ecsToCRDTWriter;
            EntitiesMap = entitiesMap;
        }
    }
}
