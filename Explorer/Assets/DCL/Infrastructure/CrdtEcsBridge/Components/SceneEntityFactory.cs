using Arch.Core;
using CRDT;
using ECS.LifeCycle.Components;
using System;

namespace CrdtEcsBridge.Components
{
    /// <summary>
    ///     Handles an archetype for special entities
    /// </summary>
    public class SceneEntityFactory : ISceneEntityFactory
    {
        public Entity Create(CRDTEntity crdtEntity, World world)
        {
            switch (crdtEntity.Id)
            {
                case 0:
                // scene root entity is not created from the SDK on demand but created once on scene construction
                case 1:
                case 2:
                    throw new NotSupportedException("Scene Root, Camera, and Player entities must be created from the relevant plugins");
                default:
                    return world.Create(RemovedComponents.CreateDefault(), crdtEntity);
            }
        }
    }
}
