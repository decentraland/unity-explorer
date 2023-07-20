using Cysharp.Threading.Tasks;
using System.Threading;

namespace Global.Dynamic
{
    public interface IRealmController
    {
        UniTask SetRealm(GlobalWorld globalWorld, string realm, CancellationToken ct);

        UniTask UnloadCurrentRealm(GlobalWorld globalWorld);
    }
}
