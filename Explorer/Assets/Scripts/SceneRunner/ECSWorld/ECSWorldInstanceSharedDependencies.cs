using Arch.Core;
using CRDT;
using SceneRunner.PublicAPI;
using System.Collections.Generic;

namespace SceneRunner.ECSWorld
{
    public readonly struct ECSWorldInstanceSharedDependencies
    {
        public readonly ISceneContentProvider ContentProvider;
        public readonly IReadOnlyDictionary<CRDTEntity, Entity> EntitiesMap;

        public ECSWorldInstanceSharedDependencies(ISceneContentProvider contentProvider, IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap)
        {
            ContentProvider = contentProvider;
            EntitiesMap = entitiesMap;
        }
    }
}
