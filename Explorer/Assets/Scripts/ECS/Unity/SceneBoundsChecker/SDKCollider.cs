using UnityEngine;

namespace ECS.Unity.SceneBoundsChecker
{
    /// <summary>
    ///     Contains information about a collider that was created from an SDK entity
    /// </summary>
    public struct SDKCollider
    {
        public readonly Collider Collider;

        private bool isActiveByEntity;
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

        public bool IsActiveBySceneBounds { get; private set; }

        /// <summary>
        ///     When the structure is created Collider is disabled by default
        /// </summary>
        /// <param name="collider"></param>
        public SDKCollider(Collider collider)
        {
            Collider = collider;
            isActiveByEntity = false;
            IsActiveBySceneBounds = false;

            ResolveColliderActivity();
        }

        public void ForceActiveBySceneBounds(bool value)
        {
            IsActiveBySceneBounds = value;
            ResolveColliderActivity();
        }

        private void ResolveColliderActivity()
        {
            Collider.enabled = isActiveByEntity && IsActiveBySceneBounds;
        }
    }
}
