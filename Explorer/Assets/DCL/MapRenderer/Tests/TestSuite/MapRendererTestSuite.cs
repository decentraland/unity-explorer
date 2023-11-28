using DCLServices.MapRenderer.CommonBehavior;
using DCLServices.MapRenderer.MapLayers;
using System.Collections.Generic;
using System.Linq;

namespace DCLServices.MapRenderer
{
    /// <summary>
    /// Contains methods and properties that must be exposed to Unit Tests without exposing them in the original file
    /// </summary>
    public partial class MapRenderer
    {
        internal IReadOnlyCollection<MapLayer> initializedLayers_Test => layers.Keys;

        // for testing purpose only
        internal IReadOnlyList<IMapLayerController> layers_Test => layers.Select(l => l.Value.MapLayerController).ToList();

        internal Dictionary<MapLayer, IMapLayerController> layersDictionary_Test => layers.ToDictionary(l => l.Key, l => l.Value.MapLayerController);

        internal void EnableLayers_Test(IMapActivityOwner owner, MapLayer mask) =>
            EnableLayers(owner, mask);

        internal void DisableLayers_Test(IMapActivityOwner owner, MapLayer mask) =>
            DisableLayers(owner, mask);
    }
}
