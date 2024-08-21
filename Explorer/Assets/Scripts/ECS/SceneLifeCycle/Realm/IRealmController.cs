using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace ECS.SceneLifeCycle.Realm
{
    public interface IRealmController
    {
        IRealmData RealmData { get; }

        UniTask SetRealmAsync(URLDomain realm, CancellationToken ct);

        UniTask RestartRealmAsync(CancellationToken ct);

        UniTask<bool> IsReachableAsync(URLDomain realm, CancellationToken ct);

        /// <summary>
        ///     Dispose everything on application quit
        /// </summary>
        void DisposeGlobalWorld();
    }
}
