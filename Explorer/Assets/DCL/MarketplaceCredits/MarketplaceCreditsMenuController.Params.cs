namespace DCL.MarketplaceCredits
{
    public partial class MarketplaceCreditsMenuController
    {
        public readonly struct Params
        {
            // Indicates if the menu was opened from a notification click
            public readonly bool IsOpenedFromNotification;

            public Params(bool isOpenedFromNotification)
            {
                IsOpenedFromNotification = isOpenedFromNotification;
            }
        }
    }
}
