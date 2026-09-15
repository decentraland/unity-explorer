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
        private const string MEDIA_URL = "https://fonts.gstatic.com/s/pacifico/v23/FwZY7-Qmy14u9lezJ96A.ttf";

        private ISceneData sceneData = null!;

        [SetUp]
        public void SetUp()
        {
            sceneData = Substitute.For<ISceneData>();

            sceneData.TryGetMediaUrl(CONTENT_FILE, out Arg.Any<URLAddress>())
                     .Returns(x =>
                      {
                          x[1] = URLAddress.FromString(CONTENT_URL);
                          return true;
                      });

            sceneData.TryGetMediaUrl(MEDIA_URL, out Arg.Any<URLAddress>())
                     .Returns(x =>
                      {
                          x[1] = URLAddress.FromString(MEDIA_URL);
                          return true;
                      });
        }

        [Test]
        public void ResolveAContentFile()
        {
            bool resolved = FontSrcResolver.TryCreateIntention(CONTENT_FILE, sceneData, out GetFontIntention intention);

            Assert.That(resolved, Is.True);
            Assert.That(intention.Kind, Is.EqualTo(FontSourceKind.File));
            Assert.That(intention.Src, Is.EqualTo(CONTENT_FILE));
            Assert.That(intention.CommonArguments.URL.Value, Is.EqualTo(CONTENT_URL));
        }

        [Test]
        public void ResolveAnAllowedMediaUrl()
        {
            bool resolved = FontSrcResolver.TryCreateIntention(MEDIA_URL, sceneData, out GetFontIntention intention);

            Assert.That(resolved, Is.True);
            Assert.That(intention.Kind, Is.EqualTo(FontSourceKind.File));
            Assert.That(intention.CommonArguments.URL.Value, Is.EqualTo(MEDIA_URL));
        }

        [Test]
        public void ResolveAFamilyNameToFontsource()
        {
            bool resolved = FontSrcResolver.TryCreateIntention("Playfair Display", sceneData, out GetFontIntention intention);

            Assert.That(resolved, Is.True);
            Assert.That(intention.Kind, Is.EqualTo(FontSourceKind.FontsourceFamily));
            Assert.That(intention.Src, Is.EqualTo("Playfair Display"));
            Assert.That(intention.CommonArguments.URL.Value, Is.EqualTo(FontsourceCatalog.API_BASE_URL + "playfair-display"));
            sceneData.DidNotReceiveWithAnyArgs().TryGetMediaUrl(default!, out Arg.Any<URLAddress>());
        }

        [TestCase("no such/file.ttf")]
        [TestCase("https://example.com/not-allowed.ttf")]
        [TestCase("$$$")]
        public void KeepTheBuiltInFontWhenNothingResolves(string fontSrc)
        {
            bool resolved = FontSrcResolver.TryCreateIntention(fontSrc, sceneData, out GetFontIntention _);

            Assert.That(resolved, Is.False);
        }

        [Test]
        public void ShareTheRequestBetweenTextsAskingForTheSameFile()
        {
            FontSrcResolver.TryCreateIntention(CONTENT_FILE, sceneData, out GetFontIntention first);
            FontSrcResolver.TryCreateIntention(CONTENT_FILE, sceneData, out GetFontIntention second);

            Assert.That(first.Equals(second), Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void KeepSourcesOfDifferentKindsApart()
        {
            var file = new GetFontIntention { Kind = FontSourceKind.File, Src = MEDIA_URL, CommonArguments = new CommonLoadingArguments(URLAddress.FromString(MEDIA_URL)) };
            var family = new GetFontIntention { Kind = FontSourceKind.FontsourceFamily, Src = MEDIA_URL, CommonArguments = new CommonLoadingArguments(URLAddress.FromString(MEDIA_URL)) };

            Assert.That(file.Equals(family), Is.False);
            Assert.That(file.GetHashCode(), Is.Not.EqualTo(family.GetHashCode()));
        }
    }
}
