
namespace DCL.MarketplaceCredits.Purchase
{
    /// <summary>
    ///     What a purchase is paid with. It decides the contract that settles it: credits and credits + MANA go
    ///     through CreditsManager.useCredits, MANA alone goes straight to the marketplace or the CollectionStore.
    /// </summary>
    public enum PaymentMethod
    {
        Credits,
        CreditsAndMana,
        Mana,
    }
}
