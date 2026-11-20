using DCL.Web3.Chains;
using System;

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

        // The newer marketplace deployment. A trade names the marketplace it was signed against, so both are valid
        // settlement targets.
        private const string POLYGON_OFFCHAIN_MARKETPLACE_V3_ADDRESS = "0xe38ef22abe871513555cba89adfe45ab4f548ada";
        private const string AMOY_OFFCHAIN_MARKETPLACE_V3_ADDRESS = "0x36fd1434a6c4b8ade80c9847c1d15033ce34488c";

        private const string POLYGON_MANA_TOKEN_ADDRESS = "0xa1c57f48f0deb89f569dfbe6e2b7f46d33606fd4";
        private const string AMOY_MANA_TOKEN_ADDRESS = "0x7ad72b9f944ea9793cf4055d88f81138cc2c63a0";

        // DCLRegistrar lives on Ethereum L1: it answers whether a NAME is still free.
        private const string MAINNET_READONLY_NETWORK = "mainnet";
        private const string SEPOLIA_READONLY_NETWORK = "sepolia";
        private const string MAINNET_DCL_REGISTRAR_ADDRESS = "0x2a187453064356c898cae034eaed119e1663acb8";
        private const string SEPOLIA_DCL_REGISTRAR_ADDRESS = "0x7518456ae93eb98f3e64571b689c626616bb7f30";

        // Across reports where a bridged deposit got to, on a separate API per environment.
        private const string MAINNET_ACROSS_DEPOSIT_STATUS_URL = "https://app.across.to/api/deposit/status";
        private const string TESTNET_ACROSS_DEPOSIT_STATUS_URL = "https://testnet.across.to/api/deposit/status";

        // EIP-712 domains, as published in decentraland-transactions (src/contracts).
        private const string CREDITS_MANAGER_EIP712_NAME = "Decentraland Credits";
        private const string CREDITS_MANAGER_EIP712_VERSION = "1.0.0";
        private const string OFFCHAIN_MARKETPLACE_EIP712_NAME = "DecentralandMarketplacePolygon";
        private const string OFFCHAIN_MARKETPLACE_EIP712_VERSION = "1.0.0";
        private const string COLLECTION_STORE_EIP712_NAME = "Decentraland Collection Store";
        private const string COLLECTION_STORE_EIP712_VERSION = "1";
        private const string POLYGON_MANA_TOKEN_EIP712_NAME = "(PoS) Decentraland MANA";
        private const string AMOY_MANA_TOKEN_EIP712_NAME = "Decentraland MANA(PoS)";
        private const string MANA_TOKEN_EIP712_VERSION = "1";

        private readonly string offChainMarketplaceV3Address;

        public int ChainId { get; }

        public string ReadonlyNetwork { get; }

        public string CreditsManagerAddress { get; }

        public string CollectionStoreAddress { get; }

        public string OffChainMarketplaceAddress { get; }

        public string EthereumReadonlyNetwork { get; }

        public string DclRegistrarAddress { get; }

        public string AcrossDepositStatusUrl { get; }

        public MetaTxContract CreditsManagerContract { get; }

        public MetaTxContract CollectionStoreContract { get; }

        public MetaTxContract ManaTokenContract { get; }

        /// <summary>
        ///     Polygon is the network that pairs with ethereum mainnet and Amoy the one that pairs with sepolia, so
        ///     the credits contracts follow the resolved <see cref="EthereumNetwork" /> rather than deciding a chain
        ///     of their own: an identity signed for one chain can never spend against the other's contracts.
        /// </summary>
        public CreditsChainConfig(EthereumNetwork ethereumNetwork)
        {
            switch (ethereumNetwork)
            {
                case EthereumNetwork.Mainnet:
                    ChainId = POLYGON_CHAIN_ID;
                    ReadonlyNetwork = POLYGON_READONLY_NETWORK;
                    CreditsManagerAddress = POLYGON_CREDITS_MANAGER_ADDRESS;
                    CollectionStoreAddress = POLYGON_COLLECTION_STORE_ADDRESS;
                    OffChainMarketplaceAddress = POLYGON_OFFCHAIN_MARKETPLACE_ADDRESS;
                    EthereumReadonlyNetwork = MAINNET_READONLY_NETWORK;
                    DclRegistrarAddress = MAINNET_DCL_REGISTRAR_ADDRESS;
                    AcrossDepositStatusUrl = MAINNET_ACROSS_DEPOSIT_STATUS_URL;
                    offChainMarketplaceV3Address = POLYGON_OFFCHAIN_MARKETPLACE_V3_ADDRESS;
                    ManaTokenContract = new MetaTxContract(POLYGON_MANA_TOKEN_ADDRESS, POLYGON_MANA_TOKEN_EIP712_NAME, MANA_TOKEN_EIP712_VERSION, true);
                    break;
                case EthereumNetwork.Sepolia:
                    ChainId = AMOY_CHAIN_ID;
                    ReadonlyNetwork = AMOY_READONLY_NETWORK;
                    CreditsManagerAddress = AMOY_CREDITS_MANAGER_ADDRESS;
                    CollectionStoreAddress = AMOY_COLLECTION_STORE_ADDRESS;
                    OffChainMarketplaceAddress = AMOY_OFFCHAIN_MARKETPLACE_ADDRESS;
                    EthereumReadonlyNetwork = SEPOLIA_READONLY_NETWORK;
                    DclRegistrarAddress = SEPOLIA_DCL_REGISTRAR_ADDRESS;
                    AcrossDepositStatusUrl = TESTNET_ACROSS_DEPOSIT_STATUS_URL;
                    offChainMarketplaceV3Address = AMOY_OFFCHAIN_MARKETPLACE_V3_ADDRESS;
                    ManaTokenContract = new MetaTxContract(AMOY_MANA_TOKEN_ADDRESS, AMOY_MANA_TOKEN_EIP712_NAME, MANA_TOKEN_EIP712_VERSION, true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(ethereumNetwork), ethereumNetwork, null);
            }

            CreditsManagerContract = new MetaTxContract(CreditsManagerAddress, CREDITS_MANAGER_EIP712_NAME, CREDITS_MANAGER_EIP712_VERSION, false);
            CollectionStoreContract = new MetaTxContract(CollectionStoreAddress, COLLECTION_STORE_EIP712_NAME, COLLECTION_STORE_EIP712_VERSION, true);
        }

        /// <summary>
        ///     Whether the address is one of the offchain marketplace deployments of this chain. A trade arrives
        ///     from the API naming its own marketplace, and that address ends up holding a MANA allowance.
        /// </summary>
        public bool IsKnownMarketplace(string address) =>
            string.Equals(address, OffChainMarketplaceAddress, StringComparison.OrdinalIgnoreCase)
            || string.Equals(address, offChainMarketplaceV3Address, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        ///     Whether the trade asks the buyer for one MANA payment and nothing else. A USD-pegged price is always
        ///     settled in MANA by the marketplace whatever address it names; a plain ERC20 price is pulled from the
        ///     address it names, so that address has to be the MANA token.
        /// </summary>
        public bool AsksOnlyForMana(TradeDto trade)
        {
            if (trade.received.Length != 1)
                return false;

            TradeAssetDto price = trade.received[0];

            return price.assetType switch
                   {
                       CreditsTradeEncoder.ASSET_TYPE_USD_PEGGED_MANA => true,
                       CreditsTradeEncoder.ASSET_TYPE_ERC20 => string.Equals(price.contractAddress, ManaTokenContract.Address, StringComparison.OrdinalIgnoreCase),
                       _ => false,
                   };
        }

        public MetaTxContract MarketplaceContract(string address) =>
            new (address, OFFCHAIN_MARKETPLACE_EIP712_NAME, OFFCHAIN_MARKETPLACE_EIP712_VERSION, false);
    }
}
