using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers;
using System.Collections.Generic;

namespace DCL.MapRenderer.CommonBehavior
{
    public interface IMapActivityOwner
    {
        IReadOnlyDictionary<MapLayer, IMapLayerParameter> LayersParameters { get; }
    }
}
