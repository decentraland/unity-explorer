using Cysharp.Threading.Tasks;
using SceneRunner.Scene;
using System.Threading;

namespace ECS.SceneLifeCycle.Components
{
    public class LiveSceneComponent
    {
        public ISceneFacade SceneFacade;

        public UniTask SceneLoop;

        public CancellationTokenSource CancellationToken;
    }
}
