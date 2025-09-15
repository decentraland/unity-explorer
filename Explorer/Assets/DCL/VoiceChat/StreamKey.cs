using System;

namespace DCL.VoiceChat
{
    [Serializable]
    internal readonly struct StreamKey : IEquatable<StreamKey>
    {
        public readonly string Identity;
        public readonly string Sid;

        public StreamKey(string identity, string sid)
        {
            this.Identity = identity;
            this.Sid = sid;
        }

        public bool Equals(StreamKey other) =>
            Identity == other.Identity
            && Sid == other.Sid;

        public override bool Equals(object? obj)
        {
            return obj is StreamKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Identity, Sid);
        }
    }
}
