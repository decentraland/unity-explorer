using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using Decentraland.Pulse;
using System.Threading;

namespace DCL.Multiplayer.Connections.Pulse
{
    public partial class PulseMultiplayerBus
    {
        private async UniTask SubscribeToProfileAnnouncementsAsync(CancellationToken ct)
        {
            await foreach (PlayerProfileVersionsAnnounced? announcement in pulseService.SubscribeAsync<PlayerProfileVersionsAnnounced>(
                               ServerMessage.MessageOneofCase.PlayerProfileVersionAnnounced, ct))
            {
                if (isDisposed)
                {
                    ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving remote profile annoucement while disposed");
                    break;
                }

                if (!peerIdCache.TryGetWallet(announcement.SubjectId, out string userId))
                {
                    ReportHub.LogError(ReportCategory.MULTIPLAYER, $"Cannot process remote profile announcement, peer not found: {announcement.SubjectId}");
                    continue;
                }

                incomingProfiles.Enqueue(userId, announcement.Version);
            }
        }
    }
}