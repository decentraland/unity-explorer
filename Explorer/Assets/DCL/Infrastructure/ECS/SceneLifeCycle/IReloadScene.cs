using Cysharp.Threading.Tasks;
using SceneRunner.Scene;
using System.Threading;

namespace ECS.SceneLifeCycle
{
    public interface IReloadScene
    {
        UniTask<ISceneFacade?> TryReloadSceneAsync(CancellationToken ct);

        UniTask<ISceneFacade?> TryReloadSceneAsync(CancellationToken ct, string sceneId);
    }
}
