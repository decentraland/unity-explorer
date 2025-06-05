using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace Global.Dynamic.RealmUrl
{
    public interface IRealmUrls
    {
        UniTask<Uri> StartingRealmAsync(CancellationToken ct);

        UniTask<Uri?> LocalSceneDevelopmentRealmAsync(CancellationToken ct);
    }

    public static class RealmUrlsExtensions
    {
        public static Uri StartingRealmBlocking(this IRealmUrls realmUrls, CancellationToken ct = default) =>
            realmUrls.StartingRealmAsync(ct).GetAwaiter().GetResult()!;

        public static Uri? LocalSceneDevelopmentRealmBlocking(this IRealmUrls realmUrls, CancellationToken ct = default) =>
            realmUrls.LocalSceneDevelopmentRealmAsync(ct).GetAwaiter().GetResult();
    }
}
