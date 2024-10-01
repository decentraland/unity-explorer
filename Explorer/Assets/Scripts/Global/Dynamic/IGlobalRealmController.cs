using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using ECS.SceneLifeCycle.Realm;
using System.Collections.Generic;
using System.Threading;

namespace Global.Dynamic
{
    public interface IGlobalRealmController : IRealmController
    {
        GlobalWorld GlobalWorld { get; set; }

        UniTask<IReadOnlyList<SceneEntityDefinition>> WaitForFixedSceneEntityDefinitionsAsync(CancellationToken ct);
    }
}
