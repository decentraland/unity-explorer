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

        private const string CREDITS_MANAGER_EIP712_NAME = "Decentraland Credits";
        private const string CREDITS_MANAGER_EIP712_VERSION = "1.0.0";

        public int ChainId { get; }

        public string ReadonlyNetwork { get; }

        public string CreditsManagerAddress { get; }

        public string CreditsManagerEip712Name => CREDITS_MANAGER_EIP712_NAME;

        public string CreditsManagerEip712Version => CREDITS_MANAGER_EIP712_VERSION;

        public CreditsChainConfig(DecentralandEnvironment environment)
        {
            if (environment is DecentralandEnvironment.Org or DecentralandEnvironment.Today)
            {
                ChainId = POLYGON_CHAIN_ID;
                ReadonlyNetwork = POLYGON_READONLY_NETWORK;
                CreditsManagerAddress = POLYGON_CREDITS_MANAGER_ADDRESS;
            }
            else
            {
                ChainId = AMOY_CHAIN_ID;
                ReadonlyNetwork = AMOY_READONLY_NETWORK;
                CreditsManagerAddress = AMOY_CREDITS_MANAGER_ADDRESS;
            }
        }
    }
}
