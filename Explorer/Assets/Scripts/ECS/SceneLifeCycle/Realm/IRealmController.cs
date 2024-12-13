using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace ECS.SceneLifeCycle.Realm
{
    public enum RealmType
    {
        GenesisCity,
        World,
        LocalScene
    }

    public interface IRealmController
    {
        RealmType Type { get; }

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
            public RealmType Type => RealmType.World;
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

    public static class RealmControllerExtensions
    {
        public static bool IsGenesis(this IRealmController realmController) =>
            realmController.Type is RealmType.GenesisCity;

        public static bool IsLocalScene(this IRealmController realmController) =>
            realmController.Type is RealmType.LocalScene;

        public static bool IsWorld(this IRealmController realmController) =>
            realmController.Type is RealmType.World;
    }
}
