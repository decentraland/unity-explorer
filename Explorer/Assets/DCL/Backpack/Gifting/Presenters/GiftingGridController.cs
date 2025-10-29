namespace DCL.Backpack.Gifting.Presenters
{
    /// <summary>
    ///     Responsibility: Fetching and displaying the user's own giftable inventory.
    ///     Implementation:
    ///     Create a new GiftingGridController that takes IWearablesProvider or IEmoteProvider.
    ///     The core difference from BackpackGridController is the parameters for the provider call. It must fetch items for
    ///     the current user's wallet address (selfProfile.ProfileAsync()) and filter for items that are actually transferable
    ///     NFTs (e.g., collectionType = OnChain | ThirdParty).
    ///     Item Interaction: The OnSelectItem event will be repurposed. Instead of showing equip/unequip options, it will
    ///     simply fire a global GiftingItemSelectedEvent on an event bus that the main GiftingController listens to.
    ///     Stock Counter: Modify BackpackItemView (or create a GiftingItemView inheriting from it) to include a GameObject and
    ///     TMP_Text for the x215 counter. The GiftingGridController will be responsible for fetching this count and setting
    ///     the text.
    /// </summary>
    public class GiftingGridController
    {
    }
}