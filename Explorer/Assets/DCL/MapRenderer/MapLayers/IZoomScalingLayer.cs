namespace DCL.MapRenderer.MapLayers
{
    public interface IZoomScalingLayer
    {
        void ApplyCameraZoom(float baseZoom, float newZoom, int zoomLevel);

        void ResetToBaseScale();
    }
}
