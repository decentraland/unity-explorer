using Arch.Core;
using ECS.TestSuite;
using NUnit.Framework;

namespace ECS.StreamableLoading.AssetBundles.Manifest.Tests
{
    public class PrepareAssetBundlesManifestParametersShould : UnitySystemTestBase<PrepareAssetBundleManifestParametersSystem>
    {
        private const string URL = "http://www.fakepath.com/";

        [SetUp]
        public void Setup()
        {
            system = new PrepareAssetBundleManifestParametersSystem(world, URL);
        }

        [Test]
        [TestCase("abcd", URL + "manifest/abcd.json")]
        [TestCase("urn:decentraland:entity:qwerty", URL + "manifest/qwerty.json")]
        [TestCase("urn:decentraland:entity:qwerty?ext=glb", URL + "manifest/qwerty.json")]
        public void FormURL(string entityId, string expected)
        {
            Entity e = world.Create(new GetAssetBundleManifestIntention(entityId));

            system.Update(0);

            Assert.That(world.TryGet(e, out GetAssetBundleManifestIntention intention), Is.True);
            Assert.That(intention.CommonArguments.URL, Is.EqualTo(expected));
        }
    }
}
