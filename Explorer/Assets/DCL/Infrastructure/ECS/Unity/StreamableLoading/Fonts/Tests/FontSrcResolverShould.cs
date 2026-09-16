using CommunicationData.URLHelpers;
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
        public void RejectExternalSourcesEvenWhenMediaUrlsAreAllowed(string fontSrc)
        {
            sceneData.TryGetMediaUrl(fontSrc, out Arg.Any<URLAddress>()).Returns(true);

            bool resolved = FontSrcResolver.TryCreateIntention(fontSrc, sceneData, out _);

            Assert.That(resolved, Is.False);
            sceneData.DidNotReceive().TryGetContentUrl(fontSrc, out Arg.Any<URLAddress>());
            sceneData.DidNotReceive().TryGetMediaUrl(fontSrc, out Arg.Any<URLAddress>());
        }

        [TestCase("no such/file.ttf")]
        [TestCase("Roboto")]
        [TestCase("Playfair Display")]
        public void KeepTheBuiltInFontWhenTheSourceIsNotSceneContent(string fontSrc)
        {
            bool resolved = FontSrcResolver.TryCreateIntention(fontSrc, sceneData, out _);

            Assert.That(resolved, Is.False);
            sceneData.DidNotReceive().TryGetMediaUrl(fontSrc, out Arg.Any<URLAddress>());
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
