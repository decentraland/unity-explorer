using CommunicationData.URLHelpers;
using DCL.Communities.EventInfo;
using NUnit.Framework;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    public class EventFeaturedItemsControllerShould
    {
        private const string MARKETPLACE_SERVER = "https://marketplace-api.decentraland.org";
        private const string COLLECTION_URN = "urn:decentraland:matic:collections-v2:0x2222222222222222222222222222222222222222";
        private const string ITEM_URN = COLLECTION_URN + ":3";

        [Test]
        public void BuildUrnQueryForItemUrn()
        {
            // Act
            bool result = EventFeaturedItemsController.TryBuildCatalogUrl(MARKETPLACE_SERVER, ITEM_URN, out URLAddress url);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual($"{MARKETPLACE_SERVER}/v3/catalog/items?first=1&urn={ITEM_URN}", url.Value);
        }

        [Test]
        public void BuildContractAddressQueryForCollectionUrn()
        {
            // Act
            bool result = EventFeaturedItemsController.TryBuildCatalogUrl(MARKETPLACE_SERVER, COLLECTION_URN, out URLAddress url);

            // Assert
            Assert.IsTrue(result);
            StringAssert.StartsWith($"{MARKETPLACE_SERVER}/v3/catalog/items?contractAddress=0x2222222222222222222222222222222222222222&first=", url.Value);
        }

        [TestCase("")]
        [TestCase("not-a-urn")]
        [TestCase("0x2222222222222222222222222222222222222222")]
        [TestCase("urn:decentraland:off-chain:base-avatars:eyebrows_00")]
        [TestCase("urn:decentraland:matic:collections-v2:0x2222222222222222222222222222222222222222:not-a-number")]
        [TestCase("urn:decentraland:matic:collections-v2:0xshort")]
        public void RejectUrnsThatAreNeitherItemNorCollection(string urn)
        {
            // Act
            bool result = EventFeaturedItemsController.TryBuildCatalogUrl(MARKETPLACE_SERVER, urn, out URLAddress url);

            // Assert
            Assert.IsFalse(result);
            Assert.AreEqual(URLAddress.EMPTY, url);
        }
    }
}
