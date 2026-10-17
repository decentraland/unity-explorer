using System.Threading;

namespace Global.Dynamic.RealmUrl
{
    public static class RealmUrlsExtensions
    {
        /// <summary>
        /// tests only
        /// </summary>
        public static string StartingRealmBlocking(this RealmUrls realmUrls, CancellationToken ct = default) =>
            realmUrls.StartingRealmAsync(ct).GetAwaiter().GetResult()!;

        /// <summary>
        /// tests only
        /// </summary>
        public static string? LocalSceneDevelopmentRealmBlocking(this RealmUrls realmUrls, CancellationToken ct = default) =>
            realmUrls.LocalSceneDevelopmentRealmAsync(ct).GetAwaiter().GetResult();
    }
}
