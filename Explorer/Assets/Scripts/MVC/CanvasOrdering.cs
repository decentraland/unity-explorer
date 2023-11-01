using System.Collections.Generic;

namespace MVC
{
    public readonly struct CanvasOrdering
    {
        public enum SortingLayer
        {
            Fullscreen,
            Popup,
            Persistent,
            Top
        }

        private static Dictionary<SortingLayer, int> sortingLayerOffsets => new()
        {
            {SortingLayer.Persistent, 0},
            {SortingLayer.Fullscreen, 200},
            {SortingLayer.Popup, 400},
            {SortingLayer.Top, 600}
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
