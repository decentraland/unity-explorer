using NUnit.Framework;

// ReSharper disable once CheckNamespace
namespace DCL.Browser.DecentralandUrls.Tests
{
    public class ClientSourceUrlExtensionsShould
    {
        [Test]
        public void OpenAQueryStringOnAPlainUrl()
        {
            Assert.AreEqual("https://decentraland.org/shop?utm_source=client",
                "https://decentraland.org/shop".WithClientSource());
        }

        // The passport builds its item link by appending a path, so the value reaching here already carries
        // one. Getting the separator wrong on this case is what would 404 the most valuable link we have.
        [Test]
        public void KeepThePathIntactWhenTaggingAnItemUrl()
        {
            Assert.AreEqual("https://decentraland.org/shop/item/0xabc/0?utm_source=client",
                "https://decentraland.org/shop/item/0xabc/0".WithClientSource());
        }

        // A '?' already present means the parameter has to join with '&' — otherwise the url carries two
        // query strings and every parameter after the second '?' is dropped by the browser.
        [Test]
        public void AppendWithAnAmpersandWhenAQueryAlreadyExists()
        {
            Assert.AreEqual("https://decentraland.org/marketplace/browse?status=on_sale&utm_source=client",
                "https://decentraland.org/marketplace/browse?status=on_sale".WithClientSource());
        }

        // These urls are built from user data that can legitimately resolve to nothing — the passport returns
        // "" for an unparseable urn. A bare "?utm_source=client" would look like a link and be opened as one.
        [Test]
        public void LeaveAnEmptyUrlAlone()
        {
            Assert.AreEqual("", "".WithClientSource());
            Assert.IsNull(((string)null).WithClientSource());
        }
    }
}
