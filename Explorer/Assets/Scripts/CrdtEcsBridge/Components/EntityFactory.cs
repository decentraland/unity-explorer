using Arch.Core;
using Arch.Core.Utils;
using CRDT;
using CrdtEcsBridge.Components.Special;
using System.Collections.Generic;

namespace CrdtEcsBridge.Components
{
    /// <summary>
    /// Handles an archetype for special entities
    /// </summary>
    public class EntityFactory : IEntityFactory
    {
        private static readonly Dictionary<CRDTEntity, ComponentType> SPECIAL_ENTITIES_ARCHETYPES = new ()
        {
            { 0, typeof(SceneRootComponent) },
            { 1, typeof(PlayerComponent) },
            { 2, typeof(CameraComponent) },
        };

        public Entity Create(CRDTEntity crdtEntity, World world)
        {
            switch (crdtEntity.Id)
            {
                case 0:
                    return world.Create(new SceneRootComponent(), crdtEntity);
                case 1:
                    return world.Create(new PlayerComponent(), crdtEntity);
                case 2:
                    return world.Create(new CameraComponent(), crdtEntity);
                default:
                    return world.Create(crdtEntity);
            }
        }
    }
}
