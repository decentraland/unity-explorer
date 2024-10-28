using Cysharp.Threading.Tasks;
using System.Threading;

namespace ECS.SceneLifeCycle
{
    public interface IReloadScene
    {
        UniTask<bool> TryReloadSceneAsync(CancellationToken ct);

        UniTask<bool> TryReloadSceneAsync(CancellationToken ct, string sceneId);
    }
}
