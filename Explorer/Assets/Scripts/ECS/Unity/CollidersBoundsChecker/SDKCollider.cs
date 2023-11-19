using UnityEngine;

namespace ECS.Unity.CollidersBoundsChecker
{
    /// <summary>
    ///     Contains information about a collider that was created from an SDK entity
    /// </summary>
    public struct SDKCollider
    {
        private bool isActiveByEntity;
        private bool isActiveBySceneBounds;

        public bool IsActiveByEntity
        {
            get => isActiveByEntity;

            internal set
            {
                if (isActiveByEntity != value)
                {
                    isActiveByEntity = value;
                    ResolveColliderActivity();
                }
            }
        }

        public bool IsActiveBySceneBounds
        {
            get => isActiveBySceneBounds;

            internal set
            {
                if (isActiveBySceneBounds != value)
                {
                    isActiveBySceneBounds = value;
                    ResolveColliderActivity();
                }
            }
        }

        private void ResolveColliderActivity()
        {
            Collider.enabled = isActiveByEntity && isActiveBySceneBounds;
        }

        public readonly Collider Collider;

        /// <summary>
        ///     When the structure is created Collider is disabled by default
        /// </summary>
        /// <param name="collider"></param>
        public SDKCollider(Collider collider)
        {
            Collider = collider;
            isActiveByEntity = false;
            isActiveBySceneBounds = false;

            ResolveColliderActivity();
        }
    }
}
