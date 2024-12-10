using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using ECS.SceneLifeCycle.Realm;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using System.Threading;

namespace Global.Dynamic
{
    public interface IGlobalRealmController : IRealmController
    {
        GlobalWorld GlobalWorld { get; set; }

        UniTask<AssetPromise<SceneEntityDefinition, GetSceneDefinition>[]> WaitForFixedScenePromisesAsync(CancellationToken ct);
        UniTask<SceneDefinitions?> WaitForStaticScenesEntityDefinitionsAsync(CancellationToken ct);
    }
}
