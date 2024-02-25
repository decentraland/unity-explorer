namespace DCL.Multiplayer.Profiles.RemoteAnnouncements
{
    public readonly struct RemoteAnnouncement
    {
        public readonly int Version;
        public readonly string WalletId;

        public RemoteAnnouncement(int version, string walletId)
        {
            this.Version = version;
            this.WalletId = walletId;
        }

        public override string ToString() =>
            $"(RemoteAnnouncement: {{ Version: {Version}, WalletId: {WalletId} }})";
    }
}
