using DCL.Web3.Chains;
using NUnit.Framework;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    [TestFixture]
    public class CreditsChainConfigShould
    {
        private const string OTHER_TOKEN = "0x3333333333333333333333333333333333333333";

        // Pinned rather than derived: these are the addresses real money moves through, and the pairing between an
        // ethereum network and its polygon counterpart is a convention no compiler enforces. Swapping the two arms
        // of the switch would otherwise ship silently.
        [Test]
        public void PairMainnetWithPolygon()
        {
            var config = new CreditsChainConfig(EthereumNetwork.Mainnet);

            Assert.AreEqual(137, config.ChainId);
            Assert.AreEqual("polygon", config.ReadonlyNetwork);
            Assert.AreEqual("0x8b3a40ca1b6f5cafc99d112a4d02e897d1fd8cc5", config.CreditsManagerAddress);
            Assert.AreEqual("0x214ffc0f0103735728dc66b61a22e4f163e275ae", config.CollectionStoreAddress);
            Assert.AreEqual("0xa40b1d129b8906888720686f3a01921ddf37716f", config.OffChainMarketplaceAddress);
            Assert.AreEqual("https://app.across.to/api/deposit/status", config.AcrossDepositStatusUrl);
        }

        [Test]
        public void PairSepoliaWithAmoy()
        {
            var config = new CreditsChainConfig(EthereumNetwork.Sepolia);

            Assert.AreEqual(80002, config.ChainId);
            Assert.AreEqual("amoy", config.ReadonlyNetwork);
            Assert.AreEqual("0x8052a560e6e6ac86eeb7e711a4497f639b322fb3", config.CreditsManagerAddress);
            Assert.AreEqual("0xe36abc9ec616c83caaa386541380829106149d68", config.CollectionStoreAddress);
            Assert.AreEqual("0x1b67d0e31eeb6b52d8eeed71d3616c2f5b33b8e7", config.OffChainMarketplaceAddress);
            Assert.AreEqual("https://testnet.across.to/api/deposit/status", config.AcrossDepositStatusUrl);
        }

        // Every address above has to belong to exactly one of the two networks; sharing one would mean a test chain
        // pointing at a production contract or the reverse.
        [Test]
        public void ShareNoAddressBetweenTheNetworks()
        {
            var mainnet = new CreditsChainConfig(EthereumNetwork.Mainnet);
            var sepolia = new CreditsChainConfig(EthereumNetwork.Sepolia);

            Assert.AreNotEqual(mainnet.ChainId, sepolia.ChainId);
            Assert.AreNotEqual(mainnet.ReadonlyNetwork, sepolia.ReadonlyNetwork);
            Assert.AreNotEqual(mainnet.CreditsManagerAddress, sepolia.CreditsManagerAddress);
            Assert.AreNotEqual(mainnet.CollectionStoreAddress, sepolia.CollectionStoreAddress);
            Assert.AreNotEqual(mainnet.OffChainMarketplaceAddress, sepolia.OffChainMarketplaceAddress);
            Assert.AreNotEqual(mainnet.AcrossDepositStatusUrl, sepolia.AcrossDepositStatusUrl);
        }

        [Test]
        public void KnowBothMarketplaceDeployments()
        {
            // Arrange
            var config = new CreditsChainConfig(EthereumNetwork.Mainnet);

            // Assert
            Assert.IsTrue(config.IsKnownMarketplace("0xa40b1d129b8906888720686f3a01921ddf37716f"));
            Assert.IsTrue(config.IsKnownMarketplace("0xE38EF22ABE871513555CBA89ADFE45AB4F548ADA"));
            Assert.IsFalse(config.IsKnownMarketplace("0x36fd1434a6c4b8ade80c9847c1d15033ce34488c"));
            Assert.IsFalse(config.IsKnownMarketplace(config.CollectionStoreAddress));
        }

        [Test]
        public void AcceptATradeAskingForASingleManaPrice()
        {
            // Arrange
            var config = new CreditsChainConfig(EthereumNetwork.Sepolia);

            // Assert: a USD-pegged price settles in MANA whatever address it names, an ERC20 one has to name MANA.
            Assert.IsTrue(config.AsksOnlyForMana(TradeReceiving(Asset(CreditsTradeEncoder.ASSET_TYPE_USD_PEGGED_MANA, OTHER_TOKEN))));
            Assert.IsTrue(config.AsksOnlyForMana(TradeReceiving(Asset(CreditsTradeEncoder.ASSET_TYPE_ERC20, config.ManaTokenContract.Address.ToUpperInvariant()))));
        }

        [Test]
        public void RefuseATradeAskingForMoreThanAManaPrice()
        {
            // Arrange
            var config = new CreditsChainConfig(EthereumNetwork.Sepolia);
            TradeAssetDto mana = Asset(CreditsTradeEncoder.ASSET_TYPE_ERC20, config.ManaTokenContract.Address);

            // Assert
            Assert.IsFalse(config.AsksOnlyForMana(TradeReceiving(Asset(CreditsTradeEncoder.ASSET_TYPE_ERC20, OTHER_TOKEN))));
            Assert.IsFalse(config.AsksOnlyForMana(TradeReceiving(mana, Asset(CreditsTradeEncoder.ASSET_TYPE_ERC721, OTHER_TOKEN))));
            Assert.IsFalse(config.AsksOnlyForMana(TradeReceiving(mana, mana)));
            Assert.IsFalse(config.AsksOnlyForMana(TradeReceiving(Asset(CreditsTradeEncoder.ASSET_TYPE_ERC721, config.ManaTokenContract.Address))));
            Assert.IsFalse(config.AsksOnlyForMana(TradeReceiving()));
        }

        private static TradeAssetDto Asset(int assetType, string contractAddress) =>
            new () { assetType = assetType, contractAddress = contractAddress, amount = "1", tokenId = "1" };

        private static TradeDto TradeReceiving(params TradeAssetDto[] received) =>
            new () { received = received };
    }
}
