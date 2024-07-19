#nullable enable

using ECS.StreamableLoading.NFTShapes.URNs;
using NUnit.Framework;

namespace ECS.StreamableLoading.Tests.URNs
{
    public class UrnsTest
    {
        [Test]
        [TestCase(
            "urn:decentraland:ethereum:erc721:0x06012c8cf97bead5deae237070f9587f8e7a266d:1631847",
            "https://opensea.decentraland.org/api/v2/chain/ethereum/contract/0x06012c8cf97bead5deae237070f9587f8e7a266d/nfts/1631847"
        )]
        [TestCase(
            "urn:decentraland:matic:erc721:0xd27a967ee4f66226d49a18d4f9fd98f4aa0b26df:9567",
            "https://opensea.decentraland.org/api/v2/chain/matic/contract/0xd27a967ee4f66226d49a18d4f9fd98f4aa0b26df/nfts/9567")
        ]
        public void UrnToUrlTest(string urn, string expectedUrl)
        {
            Assert.AreEqual(expectedUrl, new BasedURNSource().UrlOrEmpty(urn).Value);
        }
    }
}
