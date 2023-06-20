using DCL.ECSComponents;
using UnityEngine;

namespace CrdtEcsBridge.Physics
{
    public static class PhysicsLayers
    {
        private const ColliderLayer LAYER_PHYSICS = ColliderLayer.ClPhysics;
        private const ColliderLayer LAYER_POINTER = ColliderLayer.ClPointer;
        private const ColliderLayer LAYER_PHYSICS_POINTER = LAYER_PHYSICS | LAYER_POINTER;

        private const ColliderLayer NON_CUSTOM_LAYERS = ColliderLayer.ClPhysics
                                                        | ColliderLayer.ClPointer
                                                        | ColliderLayer.ClNone
                                                        | ColliderLayer.ClReserved1
                                                        | ColliderLayer.ClReserved2
                                                        | ColliderLayer.ClReserved3
                                                        | ColliderLayer.ClReserved4
                                                        | ColliderLayer.ClReserved5
                                                        | ColliderLayer.ClReserved6;

        public static readonly int DEFAULT_LAYER = LayerMask.NameToLayer("Default");
        public static readonly int ON_POINTER_EVENT_LAYER = LayerMask.NameToLayer("OnPointerEvent");
        public static readonly int CHARACTER_LAYER = LayerMask.NameToLayer("CharacterController");
        public static readonly int CHARACTER_ONLY_LAYER = LayerMask.NameToLayer("CharacterOnly");
        public static readonly int SDK_CUSTOM_LAYER = LayerMask.NameToLayer("SDKCustomLayer");

        public static bool LayerMaskHasAnySDKCustomLayer(ColliderLayer layerMask) =>
            (layerMask & ~NON_CUSTOM_LAYERS) != 0;

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
