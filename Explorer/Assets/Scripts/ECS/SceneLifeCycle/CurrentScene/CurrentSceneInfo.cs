using DCL.Utilities;
using SceneRunner.Scene;
using System;

namespace ECS.SceneLifeCycle.CurrentScene
{
    public class CurrentSceneInfo : ICurrentSceneInfo
    {
        private readonly ReactiveProperty<ICurrentSceneInfo.Status?> status = new (null);

        public bool IsPlayerStandingOnScene { get; private set; }

        public IReadonlyReactiveProperty<ICurrentSceneInfo.Status?> SceneStatus => status;

        public void Update(ISceneFacade? sceneFacade)
        {
            IsPlayerStandingOnScene = sceneFacade != null;
            status.UpdateValue(StatusFrom(sceneFacade));
        }

        private ICurrentSceneInfo.Status? StatusFrom(ISceneFacade? sceneFacade)
        {
            if (sceneFacade == null)
                return null;

            switch (sceneFacade.SceneStateProvider.State)
            {
                case SceneState.NotStarted: break;
                case SceneState.Running: break;
                case SceneState.EngineError: break;
                case SceneState.EcsError: break;
                case SceneState.JavaScriptError: break;
                case SceneState.Disposing: break;
                case SceneState.Disposed: break;
                default: throw new ArgumentOutOfRangeException();
            }

            return sceneFacade.SceneStateProvider.IsNotRunningState()
                ? ICurrentSceneInfo.Status.Crashed
                : ICurrentSceneInfo.Status.Good;
        }
    }
}
