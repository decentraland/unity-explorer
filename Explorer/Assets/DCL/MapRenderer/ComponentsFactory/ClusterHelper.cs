using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers.Cluster;
using UnityEngine.Pool;

namespace DCL.MapRenderer.ComponentsFactory
{
    public static class ClusterHelper
    {
        internal static IClusterMarker CreateClusterMarker(IObjectPool<ClusterMarkerObject> objectsPool, IMapCullingController cullingController, ICoordsUtils coordsUtils) =>
            new ClusterMarker(objectsPool, cullingController, coordsUtils);
    }
}
