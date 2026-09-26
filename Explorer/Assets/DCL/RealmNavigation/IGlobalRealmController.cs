using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using ECS.SceneLifeCycle.Realm;
using Global.Dynamic;
using System.Collections.Generic;
using System.Threading;

namespace DCL.RealmNavigation
{
    public interface IGlobalRealmController : IRealmController
    {
        GlobalWorld GlobalWorld { get; set; }

        UniTask<List<SceneEntityDefinition>> WaitForFixedScenePromisesAsync(CancellationToken ct);
        UniTask<SceneDefinitions?> WaitForStaticScenesEntityDefinitionsAsync(CancellationToken ct);
    }
}
