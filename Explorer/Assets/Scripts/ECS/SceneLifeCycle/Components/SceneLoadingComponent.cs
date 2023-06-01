using Cysharp.Threading.Tasks;
using System.Threading;

namespace ECS.SceneLifeCycle.Components
{
    public enum SceneLoadingState
    {
    }
    public class SceneLoadingComponent
    {
        public Ipfs.SceneEntityDefinition Definition;

        public UniTask Request;

        public CancellationTokenSource CancellationTokenSource;
    }
}
