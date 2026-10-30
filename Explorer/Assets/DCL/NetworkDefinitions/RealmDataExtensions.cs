using Cysharp.Threading.Tasks;
using System;

namespace ECS
{
    public static class RealmDataExtensions
    {
        private const string OFFLINE_COMMS_ADAPTER_MARKER = "offline:offline";

        public static UniTask WaitConfiguredAsync(this IRealmData realmData) =>
            UniTask.WaitUntil(() => realmData.Configured);

        public static bool IsGenesis(this IRealmData realmData) =>
            realmData.RealmType.Value is RealmKind.GenesisCity;

        public static bool IsLocalScene(this IRealmData realmData) =>
            realmData.RealmType.Value is RealmKind.LocalScene;

        public static bool IsWorld(this IRealmData realmController) =>
            realmController.RealmType.Value is RealmKind.World;

        /// <summary>
        ///     True when the realm's comms adapter is "offline:offline" (e.g. "fixed-adapter:offline:offline"):
        ///     the realm is single-player and no comms transport should connect.
        /// </summary>
        public static bool IsCommsOffline(this IRealmData realmData) =>
            realmData.CommsAdapter.Contains(OFFLINE_COMMS_ADAPTER_MARKER, StringComparison.OrdinalIgnoreCase);
    }
}
