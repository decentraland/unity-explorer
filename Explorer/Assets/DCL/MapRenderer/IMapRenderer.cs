using Arch.SystemGroups;
using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers;

namespace DCL.MapRenderer
{
    public interface IMapRenderer
    {
        IMapCameraController RentCamera(in MapCameraInput cameraInput);
        void SetSharedLayer(MapLayer mask, bool active);
        void CreateSystems(ref ArchSystemsWorldBuilder<Arch.Core.World> builder);
    }
}
