using System;

namespace DCL.Multiplayer.Connections.Pulse
{
    /// <summary>
    ///     Keeps the app domain separated from the transport assumption
    /// </summary>
    public readonly struct PeerId : IEquatable<PeerId>
    {
        public readonly uint Value;

        public PeerId(uint value)
        {
            Value = value;
        }

        public static implicit operator uint(PeerId id) =>
            id.Value;

        public bool Equals(PeerId other) =>
            Value == other.Value;

        public override bool Equals(object? obj) =>
            obj is PeerId other && Equals(other);

        public override int GetHashCode() =>
            (int)Value;
    }
}
