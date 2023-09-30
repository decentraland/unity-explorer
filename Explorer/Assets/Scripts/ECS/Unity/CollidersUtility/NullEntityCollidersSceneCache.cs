using System.Collections.Generic;
using UnityEngine;

namespace DCL.Interaction.Utility
{
    public class NullEntityCollidersSceneCache : IEntityCollidersSceneCache
    {
        public static readonly NullEntityCollidersSceneCache INSTANCE = new ();

        private NullEntityCollidersSceneCache() { }

        public void Dispose() { }

        public bool TryGetEntity(Collider collider, out ColliderEntityInfo entity)
        {
            entity = default(ColliderEntityInfo);
            return false;
        }

        public void Associate(Collider collider, ColliderEntityInfo entityInfo) { }

        public void Associate(IEnumerable<Collider> colliders, ColliderEntityInfo entityInfo) { }

        public void Remove(Collider collider) { }

        public void Remove(IEnumerable<Collider> colliders) { }
    }
}
