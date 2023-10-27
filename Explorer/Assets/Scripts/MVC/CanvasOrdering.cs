using System.Collections.Generic;

namespace MVC
{
    public readonly struct CanvasOrdering
    {
        public enum SORTING_LAYER
        {
            Fullscreen,
            Popup,
            Persistent,
            Top
        }

        private static Dictionary<SORTING_LAYER, int> sortingLayerOffsets => new()
        {
            {SORTING_LAYER.Persistent, 0},
            {SORTING_LAYER.Fullscreen, 200},
            {SORTING_LAYER.Popup, 400},
            {SORTING_LAYER.Top, 600}
        };

        public readonly SORTING_LAYER SortingLayer;
        public readonly int OrderInLayer;

        public CanvasOrdering(SORTING_LAYER sortingLayer, int orderInLayer) : this()
        {
            SortingLayer = sortingLayer;
            OrderInLayer = orderInLayer + sortingLayerOffsets[sortingLayer];
        }
    }
}
