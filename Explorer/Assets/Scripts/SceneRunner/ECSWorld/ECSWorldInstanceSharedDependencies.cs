using Arch.Core;
using CRDT;
using SceneRunner.Scene;
using System.Collections.Generic;

namespace SceneRunner.ECSWorld
{
    public readonly struct ECSWorldInstanceSharedDependencies
    {
        public readonly ISceneData SceneData;
        public readonly IReadOnlyDictionary<CRDTEntity, Entity> EntitiesMap;

        public ECSWorldInstanceSharedDependencies(ISceneData sceneData, IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap)
        {
            SceneData = sceneData;
            EntitiesMap = entitiesMap;
        }
    }
}
