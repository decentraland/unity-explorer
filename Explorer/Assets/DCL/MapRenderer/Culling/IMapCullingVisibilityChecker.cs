using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("DCL.EditMode.Tests")]
[assembly: InternalsVisibleTo("DCL.PlayMode.Tests")]

namespace DCL.MapRenderer.Culling
{
    internal interface IMapCullingVisibilityChecker
    {
        bool IsVisible<T>(T obj, CameraState camera) where T: IMapPositionProvider;
    }
}
