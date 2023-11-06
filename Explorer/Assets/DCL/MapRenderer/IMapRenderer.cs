using DCLServices.MapRenderer.MapCameraController;
using DCLServices.MapRenderer.MapLayers;

namespace DCLServices.MapRenderer
{
    public interface IMapRenderer
    {
        IMapCameraController RentCamera(in MapCameraInput cameraInput);
        void SetSharedLayer(MapLayer mask, bool active);
    }
}
