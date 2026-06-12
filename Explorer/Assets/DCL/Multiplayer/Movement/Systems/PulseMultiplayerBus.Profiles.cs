using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Web3;
using Decentraland.Pulse;

namespace DCL.Multiplayer.Movement
{
    public partial class PulseMultiplayerBus
    {
        private void HandleProfileAnnouncement(IncomingMessage message)
        {
            if (isDisposed)
            {
                ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving remote profile announcement while disposed");
                return;
            }

            PlayerProfileVersionsAnnounced announcement = message.Message.PlayerProfileVersionAnnounced;

            if (!peerIdCache.TryGetWallet(announcement.SubjectId, out Web3Address userId))
            {
                ReportHub.LogError(ReportCategory.MULTIPLAYER, $"Cannot process remote profile announcement, peer not found: {announcement.SubjectId}");
                return;
            }

            incomingProfiles.Enqueue(userId, announcement.Version);
        }
    }
}
