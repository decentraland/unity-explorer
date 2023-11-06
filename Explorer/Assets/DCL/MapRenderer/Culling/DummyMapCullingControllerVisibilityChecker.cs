namespace DCLServices.MapRenderer.Culling
{
    internal class DummyMapCullingControllerVisibilityChecker : IMapCullingVisibilityChecker
    {
        public bool IsVisible<T>(T obj, CameraState camera) where T: IMapPositionProvider =>
            true;
    }
}
