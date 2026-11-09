using CommunicationData.URLHelpers;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;

namespace SceneRuntime.Apis.Modules.AdaptationLayerHelper.Tests
{
    public class AdaptationLayerHelperWrapperShould
    {
        private ISceneData sceneData = null!;

        [SetUp]
        public void SetUp()
        {
            sceneData = Substitute.For<ISceneData>();
        }

        [Test]
        public void ResolveTextureThroughSceneMediaUrl()
        {
            var expected = URLAddress.FromString("https://peer.decentraland.org/content/contents/bafyfixture");

            sceneData.TryGetMediaUrl("images/button.png", out Arg.Any<URLAddress>())
                     .Returns(call =>
                      {
                          call[1] = expected;
                          return true;
                      });

            bool resolved = AdaptationLayerHelperWrapper.TryResolveTextureUrl(sceneData, "images/button.png", out URLAddress url);

            Assert.That(resolved, Is.True);
            Assert.That(url.Value, Is.EqualTo(expected.Value));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void RejectEmptySourceWithoutConsultingScene(string? src)
        {
            Assert.That(AdaptationLayerHelperWrapper.TryResolveTextureUrl(sceneData, src, out _), Is.False);
            sceneData.DidNotReceiveWithAnyArgs().TryGetMediaUrl(default!, out _);
        }

        [Test]
        public void RejectSourceOutsideSceneContentAndAllowedHosts()
        {
            sceneData.TryGetMediaUrl(Arg.Any<string>(), out Arg.Any<URLAddress>()).Returns(false);

            Assert.That(AdaptationLayerHelperWrapper.TryResolveTextureUrl(sceneData, "https://not-allowed.example/x.png", out _), Is.False);
        }
    }
}
