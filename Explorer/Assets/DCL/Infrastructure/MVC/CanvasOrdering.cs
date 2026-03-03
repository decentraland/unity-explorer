using System.Collections.Generic;

namespace MVC
{
    public readonly struct CanvasOrdering
    {
        public enum SortingLayer
        {
            FULLSCREEN,
            POPUP,
            PERSISTENT, //TODO: persistent needs blur handling on fullscreen and Overlay
            OVERLAY,
        }

        private static IReadOnlyDictionary<SortingLayer, int> sortingLayerOffsets => new Dictionary<SortingLayer, int>
        {
            {SortingLayer.PERSISTENT, 0},
            {SortingLayer.FULLSCREEN, 200},
            {SortingLayer.POPUP, 400},
            {SortingLayer.OVERLAY, 600},
        };

        public readonly SortingLayer Layer;
        public readonly int OrderInLayer;

        public CanvasOrdering(SortingLayer layer, int orderInLayer) : this()
        {
            Layer = layer;
            OrderInLayer = orderInLayer + sortingLayerOffsets[layer];
        }
    }
}
