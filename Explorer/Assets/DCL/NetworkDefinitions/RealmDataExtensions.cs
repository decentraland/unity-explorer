using Cysharp.Threading.Tasks;

namespace ECS
{
    public static class RealmDataExtensions
    {
        public static UniTask WaitConfiguredAsync(this IRealmData realmData) =>
            UniTask.WaitUntil(() => realmData.Configured);

        public static bool IsGenesis(this IRealmData realmData) =>
            realmData.RealmType.Value is RealmKind.GenesisCity;

        public static bool IsLocalScene(this IRealmData realmData) =>
            realmData.RealmType.Value is RealmKind.LocalScene;

        public static bool IsWorld(this IRealmData realmController) =>
            realmController.RealmType.Value is RealmKind.World;

        /// <summary>
        ///     Whether the world occupies the parcel (<paramref name="x" />, <paramref name="y" />). A world without a manifest is
        ///     assumed to occupy it.
        /// </summary>
        public static bool IsParcelOfWorld(this IRealmData realmData, int x, int y) =>
            realmData.WorldManifest.IsEmpty || realmData.WorldManifest.IsParcelOccupied(x, y);
    }
}
