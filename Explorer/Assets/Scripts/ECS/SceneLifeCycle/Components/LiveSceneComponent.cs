using Cysharp.Threading.Tasks;
using System.Threading;

namespace ECS.SceneLifeCycle.Components
{
    public class LiveSceneComponent
    {
        public UniTask SceneLoop;

        public CancellationTokenSource CancellationToken;
    }
}
