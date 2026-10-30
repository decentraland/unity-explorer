using DCL.ECSComponents;
using UnityEngine;

namespace CrdtEcsBridge.Physics
{
    public static class PhysicsLayers
    {
        private const ColliderLayer LAYER_PHYSICS = ColliderLayer.ClPhysics;
        private const ColliderLayer LAYER_POINTER = ColliderLayer.ClPointer;
        private const ColliderLayer LAYER_PHYSICS_POINTER = LAYER_PHYSICS | LAYER_POINTER;
        private const ColliderLayer LAYER_PLAYER = ColliderLayer.ClPlayer;
        private const ColliderLayer LAYER_MAIN_PLAYER = ColliderLayer.ClMainPlayer;

        /// <summary>
        ///     Bits that qualify the main player on Raycast and TriggerArea queries.
        ///     Remote avatars only match on <see cref="ColliderLayer.ClPlayer"/>.
        /// </summary>
        public const ColliderLayer PLAYER_QUALIFYING_BITS = ColliderLayer.ClPlayer | ColliderLayer.ClMainPlayer;

        private const ColliderLayer NON_CUSTOM_LAYERS = ColliderLayer.ClPhysics
                                                        | ColliderLayer.ClPointer
                                                        | ColliderLayer.ClNone
                                                        | ColliderLayer.ClPlayer
                                                        | ColliderLayer.ClMainPlayer
                                                        | ColliderLayer.ClReserved3
                                                        | ColliderLayer.ClReserved4
                                                        | ColliderLayer.ClReserved5
                                                        | ColliderLayer.ClReserved6;

        public static readonly int DEFAULT_LAYER = LayerMask.NameToLayer("Default");
        public static readonly int ON_POINTER_EVENT_LAYER = LayerMask.NameToLayer("OnPointerEvent");
        public static readonly int FLOOR_LAYER = LayerMask.NameToLayer("Floor");

        /// <summary>
        ///     Assigned to the player only
        /// </summary>
        public static readonly int CHARACTER_LAYER = LayerMask.NameToLayer("CharacterController");
        public static readonly int CHARACTER_ONLY_LAYER = LayerMask.NameToLayer("CharacterOnly");
        public static readonly int SDK_CUSTOM_LAYER = LayerMask.NameToLayer("SDKCustomLayer");
        public static readonly int OTHER_AVATARS_LAYER = LayerMask.NameToLayer("OtherAvatars");
        public static readonly int SDK_ENTITY_TRIGGER_AREA = LayerMask.NameToLayer("SDKEntityTriggerArea");
        public static readonly int SDK_AVATAR_TRIGGER_AREA = LayerMask.NameToLayer("SDKAvatarTriggerArea");
        public static readonly int SDK_AVATAR_HIT_LAYER = LayerMask.NameToLayer("SDKAvatarHit");

        public static readonly LayerMask PLAYER_ORIGIN_RAYCAST_MASK = (1 << ON_POINTER_EVENT_LAYER) | (1 << DEFAULT_LAYER) | (1 << OTHER_AVATARS_LAYER);
        public static readonly LayerMask CHARACTER_ONLY_MASK = (1 << DEFAULT_LAYER) | (1 << FLOOR_LAYER) | (1 << CHARACTER_ONLY_LAYER);
        public static readonly LayerMask PLAYER_PROXIMITY_MASK = (1 << ON_POINTER_EVENT_LAYER) | (1 << DEFAULT_LAYER);

        public static bool LayerMaskHasAnySDKCustomLayer(ColliderLayer layerMask) =>
            (layerMask & ~NON_CUSTOM_LAYERS) != 0;

        /// <summary>
        ///     True when the mask is exclusively avatar bits (CL_PLAYER / CL_MAIN_PLAYER, no other bits).
        ///     Used to route SDK colliders to the SDKAvatarHit Unity layer.
        /// </summary>
        public static bool IsAvatarOnlyMask(ColliderLayer sdkMask) =>
            sdkMask != ColliderLayer.ClNone
            && (sdkMask & PLAYER_QUALIFYING_BITS) != 0
            && (sdkMask & ~PLAYER_QUALIFYING_BITS) == 0;

        public static bool LayerMaskContainsTargetLayer(uint layerMask, uint targetLayer)
            => (layerMask & targetLayer) != 0;

        public static bool LayerMaskContainsTargetLayer(uint layerMask, ColliderLayer targetLayer) =>
            LayerMaskContainsTargetLayer(layerMask, (uint)targetLayer);

        public static bool LayerMaskContainsTargetLayer(ColliderLayer layerMask, ColliderLayer targetLayer) =>
            LayerMaskContainsTargetLayer((uint)layerMask, (uint)targetLayer);

        public static int CreateUnityLayerMaskFromSDKMask(ColliderLayer sdkMask)
        {
            // Default keeps catching SDK meshes on the Default layer regardless of which SDK bits are set.
            int unityLayerMask = 1 << DEFAULT_LAYER;

            // Player-qualifying bits include the main player capsule (CHARACTER_LAYER) and the SDK avatar-hit
            // layer (SDKAvatarHit) where SDK MeshCollider/GltfContainer colliders tagged with avatar bits live.
            if ((sdkMask & PLAYER_QUALIFYING_BITS) != 0)
            {
                unityLayerMask |= 1 << CHARACTER_LAYER;
                unityLayerMask |= 1 << SDK_AVATAR_HIT_LAYER;
            }

            unityLayerMask |= sdkMask switch
                              {
                                  ColliderLayer.ClPointer => 1 << ON_POINTER_EVENT_LAYER,
                                  ColliderLayer.ClPhysics => 1 << CHARACTER_ONLY_LAYER,
                                  _ => (1 << CHARACTER_ONLY_LAYER) | (1 << ON_POINTER_EVENT_LAYER),
                              };

            // CL_PLAYER targets any avatar: include OTHER_AVATARS_LAYER. CL_MAIN_PLAYER alone is local-only.
            if ((sdkMask & LAYER_PLAYER) == LAYER_PLAYER)
                unityLayerMask |= 1 << OTHER_AVATARS_LAYER;

            // 8 Custom SDK Layers are projected onto a single Unity layer
            if (LayerMaskHasAnySDKCustomLayer(sdkMask))
                unityLayerMask |= 1 << SDK_CUSTOM_LAYER;

            return unityLayerMask;
        }

        public static bool TryGetUnityLayerFromSDKLayer(ColliderLayer sdkMask, out int unityLayer)
        {
            if ((sdkMask & LAYER_PHYSICS_POINTER) == LAYER_PHYSICS_POINTER)
            {
                unityLayer = DEFAULT_LAYER;
                return true;
            }

            if ((sdkMask & LAYER_PHYSICS) == LAYER_PHYSICS)
            {
                unityLayer = CHARACTER_ONLY_LAYER;
                return true;
            }

            if ((sdkMask & LAYER_POINTER) == LAYER_POINTER)
            {
                unityLayer = ON_POINTER_EVENT_LAYER;
                return true;
            }

            // Avatar-only masks route to SDKAvatarHit. Player capsule passes through (matrix-disabled);
            // trigger areas and raycasts targeting avatar bits still detect them.
            if (IsAvatarOnlyMask(sdkMask))
            {
                unityLayer = SDK_AVATAR_HIT_LAYER;
                return true;
            }

            if (LayerMaskHasAnySDKCustomLayer(sdkMask))
            {
                unityLayer = SDK_CUSTOM_LAYER;
                return true;
            }

            unityLayer = 0;
            return false;
        }
    }
}
