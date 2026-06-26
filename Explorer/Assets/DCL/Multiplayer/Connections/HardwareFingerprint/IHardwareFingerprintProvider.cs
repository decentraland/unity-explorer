namespace DCL.Multiplayer.Connections.HardwareFingerprint
{
    /// <summary>
    ///     Exposes an opaque, session-stable hash of the local machine identifier. The hash is sent to
    ///     the comms-gatekeeper alongside LiveKit token requests so the backend can associate a device
    ///     with the wallets that connect from it for anti-abuse purposes. The raw hardware identifier
    ///     never leaves the machine: only the one-way SHA-256 hash is exposed.
    /// </summary>
    public interface IHardwareFingerprintProvider
    {
        string Fingerprint { get; }

        static readonly IHardwareFingerprintProvider EMPTY = new EmptyHardwareFingerprintProvider();
    }

    /// <summary>
    ///     Always yields an empty fingerprint (no-op provider).
    /// </summary>
    public sealed class EmptyHardwareFingerprintProvider : IHardwareFingerprintProvider
    {
        public string Fingerprint => string.Empty;
    }
}
