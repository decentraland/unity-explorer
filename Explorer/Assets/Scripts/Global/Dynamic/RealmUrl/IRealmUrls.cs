using Cysharp.Threading.Tasks;
using System.Threading;

namespace Global.Dynamic.RealmUrl
{
    public interface IRealmUrls
    {
        UniTask<string> StartingRealmAsync(CancellationToken ct);

        UniTask<string?> LocalSceneDevelopmentRealmAsync(CancellationToken ct);
    }

    public static class RealmUrlsExtensions
    {
        public static string StartingRealmBlocking(this IRealmUrls realmUrls, CancellationToken ct = default) =>
            realmUrls.StartingRealmAsync(ct).GetAwaiter().GetResult()!;

        public static string? LocalSceneDevelopmentRealmBlocking(this IRealmUrls realmUrls, CancellationToken ct = default) =>
            realmUrls.LocalSceneDevelopmentRealmAsync(ct).GetAwaiter().GetResult();
    }
}
