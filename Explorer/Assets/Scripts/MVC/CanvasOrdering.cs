namespace MVC
{
    public readonly struct CanvasOrdering
    {
        // These layers should be added to settings "Tags and Layers"
        public const string FULLSCREEN_SORTING_LAYER = "UI_FULLSCREEN";
        public const string POPUP_SORTING_LAYER = "UI_POPUP";

        public readonly string SortingLayer;
        public readonly int OrderInLayer;

        public CanvasOrdering(string sortingLayer, int orderInLayer)
        {
            SortingLayer = sortingLayer;
            OrderInLayer = orderInLayer;
        }
    }
}
