namespace DCL.Multiplayer.Profiles.RemoteAnnouncements
{
    public struct RemoteAnnouncement
    {
        public readonly int Version;
        public readonly string WalletId;

        public RemoteAnnouncement(int version, string walletId)
        {
            this.Version = version;
            this.WalletId = walletId;
        }
    }
}
