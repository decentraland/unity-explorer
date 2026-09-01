using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Authenticators;
using DCL.Web3.Chains;
using NUnit.Framework;
using System;

namespace DCL.Web3.Tests
{
    [TestFixture]
    public class ChainUtilsShould
    {
        [TestCase(DecentralandEnvironment.Org, EthereumNetwork.Mainnet, TestName = "org is mainnet")]
        [TestCase(DecentralandEnvironment.Zone, EthereumNetwork.Sepolia, TestName = "zone is sepolia")]
        public void PinDecentralandsOwnEnvironmentsToTheirChain(DecentralandEnvironment environment, EthereumNetwork expected)
        {
            Assert.AreEqual(expected, ChainUtils.PinnedNetworkOf(environment));
            Assert.AreEqual(expected, ChainUtils.ResolveNetwork(environment, null));
        }

        // The whole point of pinning: no argument moves a decentraland environment onto the other chain, whether it
        // names the other network, a network that does not exist, or nonsense.
        [TestCase(DecentralandEnvironment.Org, "sepolia", EthereumNetwork.Mainnet, TestName = "org asked for sepolia")]
        [TestCase(DecentralandEnvironment.Zone, "mainnet", EthereumNetwork.Sepolia, TestName = "zone asked for mainnet")]
        [TestCase(DecentralandEnvironment.Org, "MAINNET", EthereumNetwork.Mainnet, TestName = "org asked for the chain it already runs on")]
        [TestCase(DecentralandEnvironment.Zone, "amoy", EthereumNetwork.Sepolia, TestName = "zone asked for a network this flag does not name")]
        [TestCase(DecentralandEnvironment.Org, "sepolai", EthereumNetwork.Mainnet, TestName = "org asked for a misspelt network")]
        public void DiscardTheOverrideOnAPinnedEnvironment(DecentralandEnvironment environment, string ethNetworkArg, EthereumNetwork expected)
        {
            Assert.AreEqual(expected, ChainUtils.ResolveNetwork(environment, ethNetworkArg));
        }

        [Test]
        public void LeaveACustomBaseDomainUnpinned()
        {
            Assert.IsNull(ChainUtils.PinnedNetworkOf(DecentralandEnvironment.Custom));
        }

        // Blank reads as "not supplied" here. That is only safe because MainSceneLoader.CaptureEthNetworkArg
        // refuses to pass a blank value through on a custom deployment - reaching the default by mistyping the flag
        // is what would be dangerous, not the default itself.
        [TestCase(null, TestName = "absent")]
        [TestCase("", TestName = "empty")]
        [TestCase("   ", TestName = "blank")]
        public void DefaultACustomBaseDomainToMainnet(string? ethNetworkArg)
        {
            Assert.AreEqual(EthereumNetwork.Mainnet, ChainUtils.ResolveNetwork(DecentralandEnvironment.Custom, ethNetworkArg));
        }

        [TestCase("mainnet", EthereumNetwork.Mainnet, TestName = "mainnet")]
        [TestCase("sepolia", EthereumNetwork.Sepolia, TestName = "sepolia")]
        [TestCase("  SEPOLIA  ", EthereumNetwork.Sepolia, TestName = "padded and uppercase")]
        public void ParseAKnownNetwork(string value, EthereumNetwork expected)
        {
            Assert.IsTrue(ChainUtils.TryParseNetwork(value, out EthereumNetwork network));
            Assert.AreEqual(expected, network);
        }

        // The startup path validates through this before it decides whether to launch, so it must never report
        // success for something it cannot map - a false with a defaulted out value would resolve to mainnet.
        [TestCase(null, TestName = "null")]
        [TestCase("", TestName = "empty")]
        [TestCase("   ", TestName = "blank")]
        [TestCase("amoy", TestName = "a polygon network")]
        [TestCase("sepolai", TestName = "misspelt sepolia")]
        [TestCase("main net", TestName = "an inner space")]
        public void FailToParseAnythingElse(string? value)
        {
            Assert.IsFalse(ChainUtils.TryParseNetwork(value, out _));
        }

        [TestCase("sepolia", EthereumNetwork.Sepolia, TestName = "sepolia")]
        [TestCase("mainnet", EthereumNetwork.Mainnet, TestName = "mainnet")]
        [TestCase("SEPOLIA", EthereumNetwork.Sepolia, TestName = "uppercase")]
        [TestCase("Sepolia", EthereumNetwork.Sepolia, TestName = "capitalized")]
        [TestCase("  sepolia  ", EthereumNetwork.Sepolia, TestName = "surrounded by whitespace")]
        public void LetACustomBaseDomainNameItsNetwork(string ethNetworkArg, EthereumNetwork expected)
        {
            Assert.AreEqual(expected, ChainUtils.ResolveNetwork(DecentralandEnvironment.Custom, ethNetworkArg));
        }

        // Only where the value is actually read does a bad one matter, and there it must not resolve to anything:
        // falling back to the default would put the deployment on mainnet after asking for a test chain.
        [TestCase("sepolai", TestName = "misspelt sepolia")]
        [TestCase("polygon", TestName = "a polygon network, which is derived rather than named here")]
        [TestCase("amoy", TestName = "amoy")]
        [TestCase("1", TestName = "a chain id")]
        [TestCase("goerli", TestName = "a retired testnet")]
        [TestCase("mainnet sepolia", TestName = "two networks at once")]
        public void RejectAnUnknownNetworkOnACustomBaseDomain(string ethNetworkArg)
        {
            Assert.Throws<ArgumentException>(() => ChainUtils.ResolveNetwork(DecentralandEnvironment.Custom, ethNetworkArg));
        }

        [TestCase(EthereumNetwork.Mainnet, "1", "0x1", "mainnet", TestName = "mainnet ids")]
        [TestCase(EthereumNetwork.Sepolia, "11155111", "0xaa36a7", "sepolia", TestName = "sepolia ids")]
        public void MapANetworkToItsChainIds(EthereumNetwork network, string netVersion, string chainId, string networkId)
        {
            Assert.AreEqual(netVersion, ChainUtils.GetNetVersion(network));
            Assert.AreEqual(chainId, ChainUtils.GetChainId(network));
            Assert.AreEqual(networkId, ChainUtils.GetNetworkId(network));
            Assert.AreEqual(netVersion, ChainUtils.GetChainIdAsInt(network).ToString());
        }

        // The accepted --eth-network values and the network ids the client reports have to stay the same strings, so
        // an operator can read a network out of a log or an error message and pass it straight back to the flag.
        [TestCase(EthereumNetwork.Mainnet, TestName = "mainnet")]
        [TestCase(EthereumNetwork.Sepolia, TestName = "sepolia")]
        public void AcceptItsOwnNetworkIdAsAnOverride(EthereumNetwork network)
        {
            Assert.AreEqual(network, ChainUtils.ResolveNetwork(DecentralandEnvironment.Custom, ChainUtils.GetNetworkId(network)));
        }
    }
}
