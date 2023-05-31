using Cysharp.Threading.Tasks;
using SceneRunner.Scene;
using System.Threading;

namespace ECS.SceneLifeCycle.Components
{
    public enum SceneLoadingState
    {
        Spawned,
        Loading,
        Failed,
        Loaded
    }
    public class SceneLoadingComponent
    {
        public SceneLoadingState State = SceneLoadingState.Spawned;

        public Ipfs.EntityDefinition Definition;

        public UniTask<ISceneFacade> Request;

        public CancellationToken CancellationToken;
    }
}
