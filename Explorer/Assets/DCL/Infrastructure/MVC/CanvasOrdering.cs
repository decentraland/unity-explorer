namespace MVC
{
    public readonly struct CanvasOrdering
    {
        public enum SortingLayer
        {
            Fullscreen,
            Popup,
            Persistent, //TODO: persistent needs blur handling on fullscreen and Overlay
            Overlay,
        }

        public readonly SortingLayer Layer;
        public readonly int OrderInLayer;

        public CanvasOrdering(SortingLayer layer, int orderInLayer) : this()
        {
            Layer = layer;
            OrderInLayer = orderInLayer + SortingLayerOffset(layer);
        }

        private static int SortingLayerOffset(SortingLayer layer) =>
            layer switch
            {
                SortingLayer.Fullscreen => 200,
                SortingLayer.Popup => 400,
                SortingLayer.Overlay => 600,
                _ => 0,
            };
    }
}
