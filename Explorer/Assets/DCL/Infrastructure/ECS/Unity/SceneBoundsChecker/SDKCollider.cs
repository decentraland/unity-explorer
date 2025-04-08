using CrdtEcsBridge.Physics;
using DCL.ECSComponents;
using UnityEngine;

namespace ECS.Unity.SceneBoundsChecker
{
    /// <summary>
    ///     Contains information about a collider that was created from an SDK entity
    /// </summary>
    public struct SDKCollider
    {
        public readonly Collider? Collider;
        private readonly Transform? Transform;

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
        public SDKCollider(Collider? collider)
        {
            Collider = collider;
            isActiveByEntity = false;
            IsActiveBySceneBounds = false;

            if (collider != null)
            {
                Transform = collider.transform;
                Transform.hasChanged = false;
            }
            else
            {
                Transform = null;
            }
            
            ResolveColliderActivity();
        }

        public static SDKCollider NewInvalidSDKCollider() =>
            new (null);

        public void SetColliderLayer(ColliderLayer colliderLayer, out bool enabled)
        {
            GameObject colliderGameObject = Collider!.gameObject;

            enabled = PhysicsLayers.TryGetUnityLayerFromSDKLayer(colliderLayer, out int unityLayer);

            if (enabled)
                colliderGameObject.layer = unityLayer;

            IsActiveByEntity = enabled;
            Collider.enabled = enabled;
        }

        public void ForceActiveBySceneBounds(bool value)
        {
            IsActiveBySceneBounds = value;
            ResolveColliderActivity();
        }

        private void ResolveColliderActivity()
        {
            if (Collider != null)
                Collider.enabled = isActiveByEntity && IsActiveBySceneBounds;
        }

        public bool HasMoved()
        {
            if (Transform != null && Transform.hasChanged)
            {
                Transform.hasChanged = false;
                return true;
            }

            return false;
        }
       
    }
}
