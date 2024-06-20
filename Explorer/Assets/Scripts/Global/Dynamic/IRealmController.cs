using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using ECS;
using System.Threading;

namespace Global.Dynamic
{
    public interface IRealmController
    {
        GlobalWorld GlobalWorld { get; set; }
        Entity RealmEntity { get; }

        UniTask SetRealmAsync(URLDomain realm, CancellationToken ct, bool isSoloSceneLoading = false);

        UniTask<bool> IsReachableAsync(URLDomain realm, CancellationToken ct);

        IRealmData GetRealm();

        /// <summary>
        ///     Gracefully unload the current realm
        /// </summary>
        UniTask UnloadCurrentRealmAsync();

        /// <summary>
        ///     Dispose everything on application quit
        /// </summary>
        void DisposeGlobalWorld();

        void SetSoloSceneLoading(bool isSolo);
    }
}
