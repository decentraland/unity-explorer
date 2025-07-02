using System;
using System.Threading;

namespace Global.Dynamic.RealmUrl
{
    public static class RealmUrlsExtensions
    {
        public static Uri StartingRealmBlocking(this RealmUrls realmUrls, CancellationToken ct = default) =>
            realmUrls.StartingRealmAsync(ct).GetAwaiter().GetResult()!;

        public static Uri? LocalSceneDevelopmentRealmBlocking(this RealmUrls realmUrls, CancellationToken ct = default) =>
            realmUrls.LocalSceneDevelopmentRealmAsync(ct).GetAwaiter().GetResult();
    }
}
