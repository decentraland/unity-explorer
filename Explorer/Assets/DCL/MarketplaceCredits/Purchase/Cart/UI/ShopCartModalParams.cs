namespace DCL.MarketplaceCredits.Purchase.Cart.UI
{
    public readonly struct ShopCartModalParams
    {
        public const string SOURCE_SHOP_HEADER = "shop_header";
        public const string SOURCE_ADD_TO_CART = "add_to_cart";

        public readonly string Source;

        public ShopCartModalParams(string source)
        {
            Source = source;
        }
    }
}
