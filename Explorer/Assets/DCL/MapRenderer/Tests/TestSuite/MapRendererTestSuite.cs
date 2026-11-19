using DCL.MapRenderer.CommonBehavior;
using DCL.MapRenderer.MapLayers;
using System.Collections.Generic;
using System.Linq;

namespace DCL.MapRenderer
{
    /// <summary>
    /// Contains methods and properties that must be exposed to Unit Tests without exposing them in the original file
    /// </summary>
    public partial class MapRenderer
    {
        internal IReadOnlyCollection<MapLayer> initializedLayers_Test => layers.Keys;

        // for testing purpose only
        internal IReadOnlyList<IMapLayerController> layers_Test => layers.Select(l => l.Value.MapLayerController).ToList();

        internal Dictionary<MapLayer, IMapLayerController> layersDictionary_Test
        {
            get
            {
                var result = new Dictionary<MapLayer, IMapLayerController>(layers!.Count);

                foreach (KeyValuePair<MapLayer, MapLayerStatus> pair in layers)
                    result[pair.Key] = pair.Value.MapLayerController;

                return result;
            }
        }

        internal void EnableLayers_Test(IMapActivityOwner owner, MapLayer mask) =>
            EnableLayers(owner, mask);

        internal void DisableLayers_Test(IMapActivityOwner owner, MapLayer mask) =>
            DisableLayers(owner, mask);
    }
}
