using Cysharp.Threading.Tasks;
using System.Threading;

namespace ECS.SceneLifeCycle.Components
{
    public class SceneLoadingComponent
    {
        public IpfsTypes.SceneEntityDefinition Definition;

        public UniTask Request;

        public CancellationTokenSource CancellationTokenSource;
    }
}
