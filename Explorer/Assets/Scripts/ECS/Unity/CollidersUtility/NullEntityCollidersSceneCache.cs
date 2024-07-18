using System.Collections.Generic;
using UnityEngine;

namespace DCL.Interaction.Utility
{
    public class NullEntityCollidersSceneCache : IEntityCollidersSceneCache
    {
        public static readonly NullEntityCollidersSceneCache INSTANCE = new ();

        private NullEntityCollidersSceneCache() { }

        public void Dispose() { }

        public bool TryGetEntity(Collider collider, out ColliderSceneEntityInfo sceneEntity)
        {
            sceneEntity = default(ColliderSceneEntityInfo);
            return false;
        }

        public void Associate(Collider collider, ColliderSceneEntityInfo sceneEntityInfo) { }

        public void Associate(IEnumerable<Collider> colliders, ColliderSceneEntityInfo sceneEntityInfo) { }

        public void Remove(Collider collider) { }

        public void Remove(IEnumerable<Collider> colliders) { }
    }
}
