using DCL.ECSComponents;
using UnityEngine;

namespace CrdtEcsBridge.Physics
{
    public static class PhysicsLayers
    {
        public static readonly int DEFAULT_LAYER = LayerMask.NameToLayer("Default");
        public static readonly int ON_POINTER_EVENT_LAYER = LayerMask.NameToLayer("OnPointerEvent");
        public static readonly int CHARACTER_LAYER = LayerMask.NameToLayer("CharacterController");
        public static readonly int CHARACTER_ONLY_LAYER = LayerMask.NameToLayer("CharacterOnly");
        public static readonly int SDK_CUSTOM_LAYER = LayerMask.NameToLayer("SDKCustomLayer");

        private const int NON_CUSTOM_LAYERS = (int)ColliderLayer.ClPhysics
                                              | (int)ColliderLayer.ClPointer
                                              | (int)ColliderLayer.ClNone
                                              | (int)ColliderLayer.ClReserved1
                                              | (int)ColliderLayer.ClReserved2
                                              | (int)ColliderLayer.ClReserved3
                                              | (int)ColliderLayer.ClReserved4
                                              | (int)ColliderLayer.ClReserved5
                                              | (int)ColliderLayer.ClReserved6;

        public static bool IsInLayerMask(uint layerMask, int layer) =>
            (layer & layerMask) != 0;

        public static bool LayerMaskHasAnySDKCustomLayer(uint layerMask) =>
            (layerMask & ~NON_CUSTOM_LAYERS) != 0;
    }
}
