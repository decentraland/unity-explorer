namespace DCL.MarketplaceCredits.Purchase.TopUp.UI
{
    public readonly struct CreditsTopUpModalControllerParams
    {
        public const string SOURCE_HUD = "hud";
        public const string SOURCE_PURCHASE_MODAL = "purchase_modal";
        public readonly string Source;

        public CreditsTopUpModalControllerParams(string source)
        {
            Source = source;
        }
    }
}
