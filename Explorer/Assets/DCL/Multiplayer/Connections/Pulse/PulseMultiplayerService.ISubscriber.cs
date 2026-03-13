using Decentraland.Pulse;

namespace DCL.Multiplayer.Connections.Pulse
{
    public partial class PulseMultiplayerService
    {
        private interface ISubscriber
        {
            bool TryNotify(ServerMessage message);
        }
    }
}
