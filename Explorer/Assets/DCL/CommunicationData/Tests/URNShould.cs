using CommunicationData.URLHelpers;
using NUnit.Framework;

namespace DCL.CommunicationData.Tests
{
    public class URNShould
    {
        [TestCase("urn:decentraland:amoy:collections-v2:0x37ea2017c7793adbb996f1758bcb32a7fc7ef4e7:2:210624583337114373395836055367340864637790190801098222508621955083",
            "urn:decentraland:amoy:collections-v2:0x37ea2017c7793adbb996f1758bcb32a7fc7ef4e7:2")]
        [TestCase("urn:decentraland:off-chain:base-avatars:eyes_22",
            "urn:decentraland:off-chain:base-avatars:eyes_22")]
        [TestCase("urn:decentraland:matic:collections-v2:0x84a1d84f183fa0fd9b6b9cb1ed0ff1b7f5409ebb:5:526561458342785933489590138418352161594475477002745556271554888277",
            "urn:decentraland:matic:collections-v2:0x84a1d84f183fa0fd9b6b9cb1ed0ff1b7f5409ebb:5")]
        [TestCase("urn:decentraland:matic:collections-v2:0x05a4b4edfe92548cf11b6532e951dbb028922e5c:0:185",
            "urn:decentraland:matic:collections-v2:0x05a4b4edfe92548cf11b6532e951dbb028922e5c:0")]
        public void Shorten(string extendedUrnStr, string expectedUrnStr)
        {
            URN extendedUrn = extendedUrnStr;
            URN expectedUrn = expectedUrnStr;
            URN shortenedUrn = extendedUrn.Shorten();
            Assert.AreEqual(expectedUrn, shortenedUrn);
        }

        [TestCase("urn:decentraland:amoy:collections-thirdparty:back-to-the-future:amoy-eb54:earrings-9d5c")]
        [TestCase("urn:decentraland:matic:collections-thirdparty:ntr1-meta:ntr1-meta-1ef79e7b:98ac122c-523f-403f-9730-f09c992f386f")]
        [TestCase("urn:decentraland:off-chain:base-avatars:aviatorstyle")]
        public void DoNotShorten(string urnStr)
        {
            URN urn = urnStr;
            URN shortenUrn = urn.Shorten();
            Assert.AreEqual(urn, shortenUrn);
        }

        [TestCase("urn:decentraland:amoy:collections-thirdparty:back-to-the-future:amoy-eb54:earrings-9d5c:amoy:0x1d9fb685c257e74f869ba302e260c0b68f5ebb37:5",
            "urn:decentraland:amoy:collections-thirdparty:back-to-the-future:amoy-eb54:earrings-9d5c")]
        [TestCase("urn:decentraland:amoy:collections-thirdparty:back-to-the-future:amoy-eb54:panamahat-67ef:amoy:0x1d9fb685c257e74f869ba302e260c0b68f5ebb37:12",
            "urn:decentraland:amoy:collections-thirdparty:back-to-the-future:amoy-eb54:panamahat-67ef")]
        public void ShortenThirdPartyWearablesV2(string extendedUrnStr, string expectedUrnStr)
        {
            URN extendedUrn = extendedUrnStr;
            URN expectedUrn = expectedUrnStr;
            URN shortenedUrn = extendedUrn.Shorten();
            Assert.AreEqual(expectedUrn, shortenedUrn);
        }

        [TestCase("urn:decentraland:amoy:collections-thirdparty:back-to-the-future:amoy-eb54:earrings-9d5c")]
        [TestCase("urn:decentraland:amoy:collections-thirdparty:back-to-the-future:amoy-eb54:earrings-9d5c:amoy:0x1d9fb685c257e74f869ba302e260c0b68f5ebb37:5")]
        [TestCase("urn:decentraland:amoy:collections-thirdparty:back-to-the-future:amoy-eb54:fascinator-7aa2")]
        [TestCase("urn:decentraland:amoy:collections-thirdparty:back-to-the-future:amoy-eb54:panamahat-67ef")]
        public void BeThirdParty(string urnStr)
        {
            URN urn = urnStr;
            Assert.IsTrue(urn.IsThirdPartyCollection());
        }

        [TestCase("urn:decentraland:amoy:collections-v2:0xcee77a01458e39134d5cd509c9fe08e6afa40937:0:2")]
        [TestCase("urn:decentraland:amoy:collections-v2:0x37ea2017c7793adbb996f1758bcb32a7fc7ef4e7:2")]
        [TestCase("urn:decentraland:off-chain:base-avatars:BaseMale")]
        [TestCase("urn:decentraland:off-chain:base-avatars:aviatorstyle")]
        public void DontBeThirdParty(string urnStr)
        {
            URN urn = urnStr;
            Assert.IsFalse(urn.IsThirdPartyCollection());
        }
    }
}
