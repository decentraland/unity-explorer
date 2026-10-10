using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace DCL.VoiceChat.Nearby.MutePersistence
{
    public interface INearbyMuteRepository
    {
        UniTask<List<string>> GetAllMutedUsersAsync(CancellationToken ct);

        UniTask MuteUserAsync(string walletAddress, CancellationToken ct);

        UniTask UnmuteUserAsync(string walletAddress, CancellationToken ct);
    }
}
