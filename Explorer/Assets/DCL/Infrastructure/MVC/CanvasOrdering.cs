using System.Collections.Generic;

namespace MVC
{
    public readonly struct CanvasOrdering
    {
        public enum SortingLayer
        {
            Fullscreen,
            Popup,
            Persistent, //TODO: persistend needs blur handling on fullscreen and Overlay
            Overlay
        }

        private static IReadOnlyDictionary<SortingLayer, int> sortingLayerOffsets => new Dictionary<SortingLayer, int>
        {
            {SortingLayer.Persistent, 0},
            {SortingLayer.Fullscreen, 200},
            {SortingLayer.Popup, 400},
            {SortingLayer.Overlay, 600},
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
