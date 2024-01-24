using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace DCL.SceneLoadingScreens
{
    public interface ISceneTipsProvider
    {
        // TODO: in the future we may require the parcel coordinate to provide specific scene tips
        // UniTask<SceneTips> Get(Vector2Int parcelCoord, CancellationToken ct);
        UniTask<SceneTips> GetAsync(CancellationToken ct);
    }
}
