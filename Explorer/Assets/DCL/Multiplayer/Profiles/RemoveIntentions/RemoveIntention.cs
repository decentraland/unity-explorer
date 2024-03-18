namespace DCL.Multiplayer.Profiles.RemoveIntentions
{
    public readonly struct RemoveIntention
    {
        public readonly string WalletId;

        public RemoveIntention(string walletId)
        {
            WalletId = walletId;
        }

        public override string ToString() =>
            $"(RemoveIntention: {{ WalletId: {WalletId} }})";
    }
}
