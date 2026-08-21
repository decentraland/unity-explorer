using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Chains;
using System;
using System.Numerics;

namespace DCL.Web3.Authenticators
{
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

        /// <summary>
        ///     What a <c>--base-domain</c> deployment runs on when <c>--eth-network</c> says nothing: it stands in
        ///     for a production stack of its own, so an operator running a test deployment names sepolia explicitly.
        /// </summary>
        private const EthereumNetwork CUSTOM_DEFAULT_NETWORK = EthereumNetwork.Mainnet;

        /// <summary>
        ///     The one place the network is decided. Where <see cref="PinnedNetworkOf" /> names a network the
        ///     environment holds it and <paramref name="ethNetworkArg" /> is discarded, so no argument can move a
        ///     decentraland environment off its own chain. Only a <c>--base-domain</c> deployment reads the
        ///     override, and defaults to <see cref="CUSTOM_DEFAULT_NETWORK" /> without one. There, a value naming no
        ///     known network throws rather than falling back: a misspelt override must not quietly put the run on a
        ///     chain nobody asked for.
        /// </summary>
        public static EthereumNetwork ResolveNetwork(DecentralandEnvironment environment, string? ethNetworkArg)
        {
            if (PinnedNetworkOf(environment) is { } pinned)
                return pinned;

            if (string.IsNullOrWhiteSpace(ethNetworkArg))
                return CUSTOM_DEFAULT_NETWORK;

            if (TryParseNetwork(ethNetworkArg, out EthereumNetwork network))
                return network;

            throw new ArgumentException($"Unknown ethereum network '{ethNetworkArg}', expected '{NETWORK_MAINNET}' or '{NETWORK_SEPOLIA}'", nameof(ethNetworkArg));
        }

        /// <summary>
        ///     Reads an <c>--eth-network</c> value, accepting either network's own id from
        ///     <see cref="GetNetworkId" /> in any casing and with surrounding whitespace. False for null, blank and
        ///     anything else. Non-throwing so a caller can reject a bad value where it can still report and exit
        ///     cleanly, instead of letting <see cref="ResolveNetwork" /> throw somewhere it cannot.
        /// </summary>
        public static bool TryParseNetwork(string? value, out EthereumNetwork network)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                string trimmed = value.Trim();

                if (string.Equals(trimmed, NETWORK_MAINNET, StringComparison.OrdinalIgnoreCase))
                {
                    network = EthereumNetwork.Mainnet;
                    return true;
                }

                if (string.Equals(trimmed, NETWORK_SEPOLIA, StringComparison.OrdinalIgnoreCase))
                {
                    network = EthereumNetwork.Sepolia;
                    return true;
                }
            }

            network = default(EthereumNetwork);
            return false;
        }

        /// <summary>
        ///     The network an environment is fixed to, or null where the environment does not decide. Decentraland's
        ///     own environments each answer for exactly one chain - org and today for mainnet, zone for sepolia -
        ///     and that is not negotiable: their contracts, identities and backends are all on that chain, so a
        ///     client pointed at one of them signing against the other is simply wrong. A <c>--base-domain</c>
        ///     deployment is the only case the client knows nothing about, which is why it is the only one
        ///     <c>--eth-network</c> speaks for.
        /// </summary>
        public static EthereumNetwork? PinnedNetworkOf(DecentralandEnvironment environment) =>
            environment switch
            {
                DecentralandEnvironment.Org => EthereumNetwork.Mainnet,
                DecentralandEnvironment.Today => EthereumNetwork.Mainnet,
                DecentralandEnvironment.Zone => EthereumNetwork.Sepolia,
                DecentralandEnvironment.Custom => null,
                _ => throw new ArgumentOutOfRangeException(nameof(environment), environment, null),
            };

        public static string GetNetVersion(EthereumNetwork network) =>
            IsMainnet(network) ? MAINNET_NET_VERSION : SEPOLIA_NET_VERSION;

        public static string GetChainId(EthereumNetwork network) =>
            IsMainnet(network) ? MAINNET_CHAIN_ID : SEPOLIA_CHAIN_ID;

        public static BigInteger GetChainIdAsInt(EthereumNetwork network) =>
            IsMainnet(network) ? new BigInteger(MAINNET_NET_VERSION_INT) : new BigInteger(SEPOLIA_NET_VERSION_INT);

        public static string GetNetworkId(EthereumNetwork network) =>
            IsMainnet(network) ? NETWORK_MAINNET : NETWORK_SEPOLIA;

        /// <summary>
        ///     The one place a network turns into ids, so the four accessors above cannot drift apart. Exhaustive on
        ///     purpose: a network added later has to be given its own ids instead of silently answering as sepolia.
        /// </summary>
        private static bool IsMainnet(EthereumNetwork network) =>
            network switch
            {
                EthereumNetwork.Mainnet => true,
                EthereumNetwork.Sepolia => false,
                _ => throw new ArgumentOutOfRangeException(nameof(network), network, null),
            };

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
