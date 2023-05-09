using Arch.Core;
using Arch.Core.Utils;
using CRDT;
using CrdtEcsBridge.Components.Special;
using System;
using System.Collections.Generic;

namespace CrdtEcsBridge.Components
{
    /// <summary>
    /// Handles an archetype for special entities
    /// </summary>
    public class EntityFactory : IEntityFactory
    {
        private static readonly ComponentType[] DEFAULT_ARCHETYPE = Array.Empty<ComponentType>();

        private static readonly Dictionary<CRDTEntity, ComponentType[]> SPECIAL_ENTITIES_ARCHETYPES = new ()
        {
            { 0, new ComponentType[] { typeof(SceneRootComponent) } },
            { 1, new ComponentType[] { typeof(PlayerComponent) } },
            { 2, new ComponentType[] { typeof(CameraComponent) } },
        };

        public Entity Create(CRDTEntity crdtEntity, World world)
        {
            if (!SPECIAL_ENTITIES_ARCHETYPES.TryGetValue(crdtEntity, out var archetype))
                archetype = DEFAULT_ARCHETYPE;

            return world.Create(archetype);
        }
    }
}
