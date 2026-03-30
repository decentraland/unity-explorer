namespace DCL.Multiplayer.Connections.Pulse
{
    public partial class PulseMultiplayerService
    {
        private interface ISubscriber
        {
            public bool TryNotify(IncomingMessage message);
        }
    }
}
