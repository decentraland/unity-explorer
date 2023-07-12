using Arch.Core;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Global
{
    public interface IRealmController
    {
        UniTask SetRealm(World globalWorld, string realm, CancellationToken ct);

        UniTask UnloadCurrentRealm(World globalWorld);
    }
}
