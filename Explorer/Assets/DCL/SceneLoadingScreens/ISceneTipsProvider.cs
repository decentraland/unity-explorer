using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace DCL.SceneLoadingScreens
{
    public interface ISceneTipsProvider
    {
        UniTask<SceneTips> Get(Vector2Int parcelCoord, CancellationToken ct);
    }
}
