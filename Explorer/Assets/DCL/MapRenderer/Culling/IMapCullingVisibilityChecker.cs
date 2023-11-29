
namespace DCL.MapRenderer.Culling
{
    internal interface IMapCullingVisibilityChecker
    {
        bool IsVisible<T>(T obj, CameraState camera) where T: IMapPositionProvider;
    }
}
