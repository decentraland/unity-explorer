using Cysharp.Threading.Tasks;
using System.Threading;

namespace ECS.SceneLifeCycle.Components
{
    public enum SceneLoadingState
    {
        Spawned,
        Loading,
        Failed,
        Canceled
    }
    public class SceneLoadingComponent
    {
        public SceneLoadingState State = SceneLoadingState.Spawned;

        public Ipfs.EntityDefinition Definition;

        public UniTask Request;

        public CancellationTokenSource CancellationTokenSource;
    }
}
