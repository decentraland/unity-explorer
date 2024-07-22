using Cysharp.Threading.Tasks;
using System.Threading;

namespace ECS.SceneLifeCycle
{
    public interface IUnloadAllScenes
    {
        UniTask ExecuteAsync(CancellationToken ct);
    }
}
