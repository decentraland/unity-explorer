namespace DCL.Multiplayer.Connections.Pulse
{
    /// <summary>
    ///     Session-wide source of truth for whether Pulse is the active multiplayer transport.
    ///     Seeded from the PULSE feature flag at construction. Set to inactive (one-way) only when
    ///     the start-up connection is unreachable — a full fallback to LiveKit, after which the client
    ///     behaves as if Pulse were never present. Never flipped at runtime: runtime reconnection
    ///     failures keep retrying without changing this value.
    /// </summary>
    public sealed class PulseActivation
    {
        private volatile bool isActive;

        public bool IsActive => isActive;

        public PulseActivation(bool initiallyActive)
        {
            isActive = initiallyActive;
        }

        public void Deactivate() =>
            isActive = false;
    }
}
