using System;

namespace DCL.Multiplayer.Profiles.RemoteAnnouncements
{
    public readonly struct RemoteAnnouncement : IEquatable<RemoteAnnouncement>
    {
        public readonly int Version;
        public readonly string WalletId;

        public RemoteAnnouncement(int version, string walletId)
        {
            Version = version;
            WalletId = walletId;
        }

        public override string ToString() =>
            $"(RemoteAnnouncement: {{ Version: {Version}, WalletId: {WalletId} }})";

        public bool Equals(RemoteAnnouncement other) =>
            Version == other.Version && WalletId == other.WalletId;

        public override bool Equals(object? obj) =>
            obj is RemoteAnnouncement other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Version, WalletId);
    }
}
