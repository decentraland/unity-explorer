namespace DCL.Multiplayer.Connections.HardwareFingerprint
{
    /// <summary>
    ///     Opaque one-way hash of the local machine id, sent to comms-gatekeeper with LiveKit token
    ///     requests for device-level anti-abuse. The raw identifier never leaves the machine.
    /// </summary>
    public interface IHardwareFingerprintProvider
    {
        string Fingerprint { get; }

        static readonly IHardwareFingerprintProvider EMPTY = new EmptyHardwareFingerprintProvider();
    }

    public sealed class EmptyHardwareFingerprintProvider : IHardwareFingerprintProvider
    {
        public string Fingerprint => string.Empty;
    }
}
