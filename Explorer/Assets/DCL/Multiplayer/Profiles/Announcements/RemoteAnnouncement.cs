namespace DCL.Multiplayer.Profiles.RemoteAnnouncements
{
    public readonly struct RemoteAnnouncement
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
    }
}
