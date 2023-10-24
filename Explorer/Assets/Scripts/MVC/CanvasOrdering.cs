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

        public readonly SORTING_LAYER SortingLayer;
        public readonly int OrderInLayer;

        public CanvasOrdering(SORTING_LAYER sortingLayer, int orderInLayer)
        {
            SortingLayer = sortingLayer;
            OrderInLayer = orderInLayer;
        }
    }
}
