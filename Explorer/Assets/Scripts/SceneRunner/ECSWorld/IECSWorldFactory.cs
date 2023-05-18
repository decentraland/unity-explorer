using Arch.Core;
using CRDT;
using System.Collections.Generic;

namespace SceneRunner.ECSWorld
{
    public interface IECSWorldFactory
    {
        /// <summary>
        /// Create a new instance of the ECS world, all its systems and attach them to the player loop
        /// </summary>
        /// add per world dependencies here
        ECSWorldFacade CreateWorld(Dictionary<CRDTEntity, Entity> entitiesMap = null);
    }
}
