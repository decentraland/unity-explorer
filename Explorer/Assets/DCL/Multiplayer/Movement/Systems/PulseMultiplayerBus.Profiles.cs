using DCL.Diagnostics;
using Decentraland.Pulse;

namespace DCL.Multiplayer.Connections.Pulse
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

            if (!peerIdCache.TryGetWallet(announcement.SubjectId, out string userId))
            {
                ReportHub.LogError(ReportCategory.MULTIPLAYER, $"Cannot process remote profile announcement, peer not found: {announcement.SubjectId}");
                return;
            }

            incomingProfiles.Enqueue(userId, announcement.Version);
        }
    }
}
