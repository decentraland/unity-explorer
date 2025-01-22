using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Multiplayer.Connectivity
{
    public interface IOnlineUsersProvider
    {
        UniTask<IReadOnlyCollection<OnlineUserData>> GetAsync(CancellationToken ct);
    }
}
