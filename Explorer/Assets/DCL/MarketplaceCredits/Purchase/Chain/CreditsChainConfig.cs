using DCL.Multiplayer.Connections.DecentralandUrls;

namespace DCL.MarketplaceCredits.Purchase
{
    public class CreditsChainConfig
    {
        private const int POLYGON_CHAIN_ID = 137;
        private const int AMOY_CHAIN_ID = 80002;

        private const string POLYGON_READONLY_NETWORK = "polygon";
        private const string AMOY_READONLY_NETWORK = "amoy";

        private const string POLYGON_CREDITS_MANAGER_ADDRESS = "0x8b3a40ca1b6f5cafc99d112a4d02e897d1fd8cc5";
        private const string AMOY_CREDITS_MANAGER_ADDRESS = "0x8052a560e6e6ac86eeb7e711a4497f639b322fb3";

        // CollectionStore — where a PRIMARY item with no trade is minted from (useCredits' external call target
        // for a mint, the way the marketplace is for a trade).
        private const string POLYGON_COLLECTION_STORE_ADDRESS = "0x214ffc0f0103735728dc66b61a22e4f163e275ae";
        private const string AMOY_COLLECTION_STORE_ADDRESS = "0xe36abc9ec616c83caaa386541380829106149d68";

        // The offchain marketplace. Not used to buy a mint — it is the contract that exposes
        // `manaUsdAggregator()`, so it is the address the MANA/USD rate is read THROUGH. A trade already names
        // its own marketplace; a mint has none, and a collection contract does not expose that method at all.
        private const string POLYGON_OFFCHAIN_MARKETPLACE_ADDRESS = "0xa40b1d129b8906888720686f3a01921ddf37716f";
        private const string AMOY_OFFCHAIN_MARKETPLACE_ADDRESS = "0x1b67d0e31eeb6b52d8eeed71d3616c2f5b33b8e7";

        private const string CREDITS_MANAGER_EIP712_NAME = "Decentraland Credits";
        private const string CREDITS_MANAGER_EIP712_VERSION = "1.0.0";

        public int ChainId { get; }

        public string ReadonlyNetwork { get; }

        public string CreditsManagerAddress { get; }

        public string CollectionStoreAddress { get; }

        public string OffChainMarketplaceAddress { get; }

        public string CreditsManagerEip712Name => CREDITS_MANAGER_EIP712_NAME;

        public string CreditsManagerEip712Version => CREDITS_MANAGER_EIP712_VERSION;

        public CreditsChainConfig(DecentralandEnvironment environment)
        {
            if (environment is DecentralandEnvironment.Org or DecentralandEnvironment.Today)
            {
                ChainId = POLYGON_CHAIN_ID;
                ReadonlyNetwork = POLYGON_READONLY_NETWORK;
                CreditsManagerAddress = POLYGON_CREDITS_MANAGER_ADDRESS;
                CollectionStoreAddress = POLYGON_COLLECTION_STORE_ADDRESS;
                OffChainMarketplaceAddress = POLYGON_OFFCHAIN_MARKETPLACE_ADDRESS;
            }
            else
            {
                ChainId = AMOY_CHAIN_ID;
                ReadonlyNetwork = AMOY_READONLY_NETWORK;
                CreditsManagerAddress = AMOY_CREDITS_MANAGER_ADDRESS;
                CollectionStoreAddress = AMOY_COLLECTION_STORE_ADDRESS;
                OffChainMarketplaceAddress = AMOY_OFFCHAIN_MARKETPLACE_ADDRESS;
            }
        }
    }
}
