using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components.Special;
using ECS.LifeCycle.Components;
using System;

namespace CrdtEcsBridge.Components
{
    /// <summary>
    /// Handles an archetype for special entities
    /// </summary>
    public class EntityFactory : IEntityFactory
    {
        public Entity Create(CRDTEntity crdtEntity, World world)
        {
            switch (crdtEntity.Id)
            {
                case 0:
                    return world.Create(new SceneRootComponent(), crdtEntity);
                case 1:
                case 2:
                    throw new NotSupportedException("Camera and Player entities must be created from the relevant plugins");
                default:
                    return world.Create(RemovedComponents.CreateDefault(), crdtEntity);
            }
        }
    }
}
