using DCL.Diagnostics;
using Pulse.Transport;

namespace DCL.Multiplayer.Connections.Pulse
{
    public partial class PulseMultiplayerBus
    {
        private bool HandleDisconnect(DisconnectReason reason)
        {
            ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Pulse transport disconnected: {reason}");

            RemoveAllPeers();

            return reason is DisconnectReason.NONE or DisconnectReason.GRACEFUL;
        }
    }
}
