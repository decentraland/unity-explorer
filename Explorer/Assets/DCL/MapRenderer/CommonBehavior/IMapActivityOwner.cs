using DCLServices.MapRenderer.MapCameraController;
using DCLServices.MapRenderer.MapLayers;
using System.Collections.Generic;

namespace DCLServices.MapRenderer.CommonBehavior
{
    public interface IMapActivityOwner
    {
        IReadOnlyDictionary<MapLayer, IMapLayerParameter> LayersParameters { get; }
    }
}
