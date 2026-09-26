using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Web3;
using Decentraland.Pulse;

namespace DCL.Multiplayer.Movement
{
    public partial class PulseMultiplayerBus
    {
        private const string PROFILE_ANNOUNCEMENT_MESSAGE = "profile announcement";

        private void HandleProfileAnnouncement(IncomingMessage message)
        {
            if (isDisposed)
            {
                ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving remote profile announcement while disposed");
                return;
            }

            PlayerProfileVersionsAnnounced announcement = message.Message.PlayerProfileVersionAnnounced;

            if (!TryGetWalletInCurrentRealm(announcement.SubjectId, PROFILE_ANNOUNCEMENT_MESSAGE, out Web3Address userId))
                return;

            incomingProfiles.Enqueue(userId, announcement.Version);
        }
    }
}
