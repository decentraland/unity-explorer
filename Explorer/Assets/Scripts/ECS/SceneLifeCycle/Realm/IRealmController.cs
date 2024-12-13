using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
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
    }

    public static class RealmControllerExtensions
    {
        public static bool IsGenesis(this IRealmController realmController) =>
            realmController.Type is RealmType.GenesisCity;
        
        public static bool IsLocalScene(this IRealmController realmController) =>
            realmController.Type is RealmType.LocalScene;
    }
}
