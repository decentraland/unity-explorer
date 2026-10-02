using CommunicationData.URLHelpers;
using DCL.Ipfs;
using ECS.StreamableLoading.Common.Components;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;

namespace ECS.StreamableLoading.Fonts.Tests
{
    [TestFixture]
    public class FontSrcResolverShould
    {
        private const string CONTENT_FILE = "fonts/Lobster-Regular.ttf";
        private const string CONTENT_URL = "https://peer.decentraland.org/content/contents/bafyfont";
        private const string CONTENT_HASH = "bafyfont";

        private ISceneData sceneData = null!;

        [SetUp]
        public void SetUp()
        {
            sceneData = Substitute.For<ISceneData>();
            sceneData.TryGetContentUrl(CONTENT_FILE, out Arg.Any<URLAddress>())
                     .Returns(x =>
                      {
                          x[1] = URLAddress.FromString(CONTENT_URL);
                          return true;
                      });
        }

        [Test]
        public void ResolveAContentFile()
        {
            bool resolved = FontSrcResolver.TryCreateIntention(CONTENT_FILE, sceneData, out GetFontIntention intention);

            Assert.That(resolved, Is.True);
            Assert.That(intention.Src, Is.EqualTo(CONTENT_FILE));
            Assert.That(intention.CommonArguments.URL.Value, Is.EqualTo(CONTENT_URL));
        }

        [TestCase("https://example.com/font.ttf")]
        [TestCase("http://example.com/font.ttf")]
        [TestCase("//example.com/font.ttf")]
        [TestCase("file:///tmp/font.ttf")]
        [TestCase("data:font/ttf;base64,AAAA")]
        [TestCase("https://cdn.example.com/fonts/roboto/latin-400-normal.ttf")]
        public void RejectExternalSourcesEvenWhenMediaUrlsAreAllowed(string fontSrc)
        {
            sceneData.TryGetMediaUrl(fontSrc, out Arg.Any<URLAddress>()).Returns(true);

            bool resolved = FontSrcResolver.TryCreateIntention(fontSrc, sceneData, out _);

            Assert.That(resolved, Is.False);
            sceneData.DidNotReceive().TryGetContentUrl(fontSrc, out Arg.Any<URLAddress>());
            sceneData.DidNotReceive().TryGetMediaUrl(fontSrc, out Arg.Any<URLAddress>());
        }

        [TestCase("no such/file.ttf")]
        [TestCase("missing.ttf")]
        [TestCase("fonts\\missing.otf")]
        [TestCase("Font$Name")]
        [TestCase("Roboto")]
        [TestCase("Playfair Display")]
        public void RejectSourcesThatAreNotSceneContentFiles(string fontSrc)
        {
            bool resolved = FontSrcResolver.TryCreateIntention(fontSrc, sceneData, out _);

            Assert.That(resolved, Is.False);
            sceneData.DidNotReceive().TryGetMediaUrl(fontSrc, out Arg.Any<URLAddress>());
        }

        [Test]
        public void ResolveAContentFileNamedLikeAFamily()
        {
            sceneData.TryGetContentUrl("Roboto", out Arg.Any<URLAddress>())
                     .Returns(x =>
                      {
                          x[1] = URLAddress.FromString(CONTENT_URL);
                          return true;
                      });

            bool resolved = FontSrcResolver.TryCreateIntention("Roboto", sceneData, out GetFontIntention intention);

            Assert.That(resolved, Is.True);
            Assert.That(intention.CommonArguments.URL.Value, Is.EqualTo(CONTENT_URL));
        }

        [Test]
        public void PreferTheConvertedBundleWhenTheManifestListsTheFont()
        {
            AssetBundleManifestVersion manifest = WithSceneManifest($"{CONTENT_HASH}_0123456789abcdef0123456789abcdef_windows");

            FontSrcResolver.TryCreateIntention(CONTENT_FILE, sceneData, out GetFontIntention intention);

            Assert.That(intention.AssetBundleHash, Is.EqualTo(CONTENT_HASH));
            Assert.That(intention.AssetBundleListed, Is.True);
            Assert.That(intention.AssetBundleManifest, Is.SameAs(manifest));
            Assert.That(intention.SceneId, Is.EqualTo("scene"));
            Assert.That(intention.CommonArguments.URL.Value, Is.EqualTo(CONTENT_URL), "the content URL identifies the request");
        }

        [Test]
        public void MarkTheBundleUnlistedWhenTheManifestListsNoBundleForIt()
        {
            WithSceneManifest("bafyotherfile_0123456789abcdef0123456789abcdef_windows");

            FontSrcResolver.TryCreateIntention(CONTENT_FILE, sceneData, out GetFontIntention intention);

            Assert.That(intention.AssetBundleListed, Is.False);
        }

        private AssetBundleManifestVersion WithSceneManifest(params string[] files)
        {
            var manifest = AssetBundleManifestVersion.CreateFromFallback("v49", "2026-05-01");
            manifest.InjectDepsDigests(files);
            sceneData.SceneEntityDefinition.Returns(new SceneEntityDefinition("scene", new SceneMetadata()) { assetBundleManifestVersion = manifest });
            sceneData.TryGetHash(CONTENT_FILE, out Arg.Any<string>())
                     .Returns(x =>
                      {
                          x[1] = CONTENT_HASH;
                          return true;
                      });
            return manifest;
        }

        [Test]
        public void ShareRequestsForTheSameResolvedContent()
        {
            FontSrcResolver.TryCreateIntention(CONTENT_FILE, sceneData, out GetFontIntention first);
            var alias = new GetFontIntention { Src = "fonts/alias.ttf", CommonArguments = new CommonLoadingArguments(CONTENT_URL) };

            Assert.That(first.Equals(alias), Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(alias.GetHashCode()));
        }

        [Test]
        public void KeepDifferentContentApart()
        {
            FontSrcResolver.TryCreateIntention(CONTENT_FILE, sceneData, out GetFontIntention first);
            var other = new GetFontIntention { Src = CONTENT_FILE, CommonArguments = new CommonLoadingArguments("https://peer.decentraland.org/content/contents/otherfont") };

            Assert.That(first.Equals(other), Is.False);
        }
    }
}
