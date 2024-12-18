using Cysharp.Threading.Tasks;
using System.Threading;

namespace Global.Dynamic.RealmUrl
{
    public interface IRealmUrls
    {
        UniTask<string> StartingRealmAsync(CancellationToken ct);

        UniTask<string?> LocalSceneDevelopmentRealmAsync(CancellationToken ct);
    }
}
