using DCL.MapRenderer.Culling;

namespace DCL.MapRenderer.MapLayers.Cluster
{
    public interface IClusterableMarker : IMapPositionProvider
    {
        void OnBecameInvisible();

        void OnBecameVisible();

        void ResetScale(float scale);
    }
}
