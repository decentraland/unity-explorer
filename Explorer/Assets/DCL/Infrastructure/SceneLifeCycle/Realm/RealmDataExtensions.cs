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
    }
}
