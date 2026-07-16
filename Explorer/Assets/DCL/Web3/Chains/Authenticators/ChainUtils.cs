using DCL.Multiplayer.Connections.DecentralandUrls;
using System.Numerics;

namespace DCL.Web3.Authenticators
{
    // TODO: this is a temporary thing until we solve the network in a better way (probably it should be parametrized)
    public static class ChainUtils
    {
        private const string NETWORK_MAINNET = "mainnet";
        private const string NETWORK_SEPOLIA = "sepolia";

        private const string MAINNET_NET_VERSION = "1";
        private const string SEPOLIA_NET_VERSION = "11155111";

        private const int MAINNET_NET_VERSION_INT = 1;
        private const int SEPOLIA_NET_VERSION_INT = 11155111;

        private const string MAINNET_CHAIN_ID = "0x1";
        private const string SEPOLIA_CHAIN_ID = "0xaa36a7";

        public static string GetNetVersion(DecentralandEnvironment env) =>
            env is DecentralandEnvironment.Org or DecentralandEnvironment.Today ? MAINNET_NET_VERSION : SEPOLIA_NET_VERSION;

        public static string GetChainId(DecentralandEnvironment env) =>
            env is DecentralandEnvironment.Org or DecentralandEnvironment.Today ? MAINNET_CHAIN_ID : SEPOLIA_CHAIN_ID;

        public static BigInteger GetChainIdAsInt(DecentralandEnvironment environment) =>
            environment is DecentralandEnvironment.Org or DecentralandEnvironment.Today ? new BigInteger(MAINNET_NET_VERSION_INT) : new BigInteger(SEPOLIA_NET_VERSION_INT);

        public static string GetNetworkId(DecentralandEnvironment env) =>
            env is DecentralandEnvironment.Org or DecentralandEnvironment.Today ? NETWORK_MAINNET : NETWORK_SEPOLIA;

        public static string GetNetworkNameById(int chainId) =>
            chainId switch
            {
                1 => "Ethereum Mainnet",
                11155111 => "Sepolia",
                _ => $"Chain {chainId}",
            };

        public static int? GetChainIdFromReadonlyNetwork(string? networkName)
        {
            if (string.IsNullOrEmpty(networkName))
                return null;

            string lowerName = networkName.ToLowerInvariant();

            int? result = lowerName switch
                          {
                              "polygon" => 137, // Polygon Mainnet
                              "amoy" => 80002, // Polygon Amoy Testnet
                              "ethereum" => 1, // Ethereum Mainnet
                              "sepolia" => 11155111, // Ethereum Sepolia Testnet
                              "mainnet" => 1, // Alias for Ethereum Mainnet
                              _ => null,
                          };

            return result;
        }
    }
}
