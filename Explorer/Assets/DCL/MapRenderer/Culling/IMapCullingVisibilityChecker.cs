using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("DCL.EditMode.Tests")]

namespace DCL.MapRenderer.Culling
{
    internal interface IMapCullingVisibilityChecker
    {
        bool IsVisible<T>(T obj, CameraState camera) where T: IMapPositionProvider;
    }
}
