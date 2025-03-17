using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace ECS.SceneLifeCycle.Realm
{
    public interface IRealmController
    {
        URLDomain? CurrentDomain { get; }

        IRealmData RealmData { get; }

        UniTask SetRealmAsync(URLDomain realm, CancellationToken ct);

        UniTask RestartRealmAsync(CancellationToken ct);

        UniTask<bool> IsReachableAsync(URLDomain realm, CancellationToken ct);

        /// <summary>
        ///     Dispose everything on application quit
        /// </summary>
        void DisposeGlobalWorld();

        class Fake : IRealmController
        {
            public RealmKind Kind => RealmKind.World;
            public URLDomain? CurrentDomain => URLDomain.EMPTY;
            public IRealmData RealmData => new IRealmData.Fake();

            public UniTask SetRealmAsync(URLDomain realm, CancellationToken ct) =>

                //ignore
                UniTask.CompletedTask;

            public UniTask RestartRealmAsync(CancellationToken ct) =>

                //ignore
                UniTask.CompletedTask;

            public async UniTask<bool> IsReachableAsync(URLDomain realm, CancellationToken ct) =>
                false;

            public void DisposeGlobalWorld()
            {
                //ignore
            }
        }
    }
}
