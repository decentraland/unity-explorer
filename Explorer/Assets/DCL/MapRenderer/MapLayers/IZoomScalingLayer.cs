namespace DCL.MapRenderer.MapLayers
{
    public interface IZoomScalingLayer
    {
        // Property to block the zoom on the layer
        bool ZoomBlocked { get; set; }

        void ApplyCameraZoom(float baseZoom, float newZoom, int zoomLevel);

        void ResetToBaseScale();
    }
}
