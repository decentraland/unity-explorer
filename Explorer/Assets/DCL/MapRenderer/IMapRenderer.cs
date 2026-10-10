using Arch.SystemGroups;
using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers;
using UnityEngine;

namespace DCL.MapRenderer
{
    public interface IMapRenderer
    {
        IMapCameraController RentCamera(in MapCameraInput cameraInput);
        void SetSharedLayer(MapLayer mask, bool active);

        /// <summary>
        ///     Shows the map of the world <paramref name="worldName" />, whose parcels lie within <paramref name="parcelBounds" /> (max exclusive) when known,
        ///     and hides the layers that only describe Genesis City
        /// </summary>
        void ShowWorld(string worldName, RectInt? parcelBounds);

        /// <summary>
        ///     Shows the map of Genesis City, the default
        /// </summary>
        void ShowGenesisCity();
        void CreateSystems(ref ArchSystemsWorldBuilder<Arch.Core.World> builder);
    }
}
