using System;

namespace DCL.VoiceChat
{
    [Serializable]
    internal readonly struct StreamKey : IEquatable<StreamKey>
    {
        public readonly string Identity;

        public StreamKey(string identity)
        {
            this.Identity = identity;
        }

        public bool Equals(StreamKey other) =>
            Identity == other.Identity;

        public override bool Equals(object? obj)
        {
            return obj is StreamKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Identity);
        }
    }
}
