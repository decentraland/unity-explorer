using Arch.Core;
using AssetManagement;
using CommunicationData.URLHelpers;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using UnityEngine;
using UnityEngine.TestTools;

namespace ECS.StreamableLoading.AssetBundles.Tests
{
    [TestFixture]
    public class PrepareAssetBundleLoadingParametersSystemShould : UnitySystemTestBase<PrepareAssetBundleLoadingParametersSystem>
    {
        [SetUp]
        public void SetUp()
        {
            path = URLDomain.FromString($"file://{Application.dataPath}" + "/../TestResources/AssetBundles/");
            sceneData = Substitute.For<ISceneData>();
            system = new PrepareAssetBundleLoadingParametersSystem(world, sceneData, path);
        }

        private ISceneData sceneData;
        private URLDomain path;

        private static readonly URLDomain FAKE_AB_PATH = URLDomain.FromString("http://www.fakepath.com/");

        [Test]
        public void LoadFromEmbeddedFirst()
        {
            LogAssert.ignoreFailingMessages = true;

            var intent = GetAssetBundleIntention.FromHash(typeof(GameObject), "TEST", permittedSources: AssetSource.EMBEDDED | AssetSource.WEB);

            Entity e = world.Create(intent, new StreamableLoadingState());

            system.Update(0);

            intent = world.Get<GetAssetBundleIntention>(e);

            Assert.That(intent.CommonArguments.Attempts, Is.EqualTo(1));
            Assert.That(intent.CommonArguments.CurrentSource, Is.EqualTo(AssetSource.EMBEDDED));
            Assert.That(intent.CommonArguments.URL, Is.EqualTo(path + "TEST"));
        }

        [Test]
        public void LoadFromWebWithOldPath()
        {
            LogAssert.ignoreFailingMessages = true;
            string version = "v" + (SceneAssetBundleManifest.ASSET_BUNDLE_VERSION_REQUIRES_HASH - 1);

            sceneData.AssetBundleManifest.Returns(new SceneAssetBundleManifest(FAKE_AB_PATH, version, new[] { "abcd" }, "hash", "04_10_2024"));

            var intent = GetAssetBundleIntention.FromHash(typeof(GameObject), "abcd", permittedSources: AssetSource.WEB);
            Entity e = world.Create(intent, new StreamableLoadingState());
            system.Update(0);

            intent = world.Get<GetAssetBundleIntention>(e);

            Assert.That(intent.CommonArguments.Attempts, Is.EqualTo(StreamableLoadingDefaults.ATTEMPTS_COUNT));
            Assert.That(intent.CommonArguments.CurrentSource, Is.EqualTo(AssetSource.WEB));
            Assert.That(intent.CommonArguments.URL, Is.EqualTo($"http://www.fakepath.com/{version}/abcd"));
            Assert.That(intent.cacheHash, Is.Not.Null);
        }
        
        [Test]
        public void LoadFromWebWithNewPath()
        {
            LogAssert.ignoreFailingMessages = true;
            string version = "v" + SceneAssetBundleManifest.ASSET_BUNDLE_VERSION_REQUIRES_HASH;
            sceneData.AssetBundleManifest.Returns(new SceneAssetBundleManifest(FAKE_AB_PATH, version, new[] { "abcd" }, "hash", "04_10_2024"));

            var intent = GetAssetBundleIntention.FromHash(typeof(GameObject), "abcd", permittedSources: AssetSource.WEB);
            Entity e = world.Create(intent, new StreamableLoadingState());
            system.Update(0);

            intent = world.Get<GetAssetBundleIntention>(e);

            Assert.That(intent.CommonArguments.Attempts, Is.EqualTo(StreamableLoadingDefaults.ATTEMPTS_COUNT));
            Assert.That(intent.CommonArguments.CurrentSource, Is.EqualTo(AssetSource.WEB));
            Assert.That(intent.CommonArguments.URL, Is.EqualTo($"http://www.fakepath.com/{version}/hash/abcd"));
            Assert.That(intent.cacheHash, Is.Not.Null);
        }

        [Test]
        public void FailIfAbsentInManifestOldHash()
        {
            LogAssert.ignoreFailingMessages = true;

            sceneData.AssetBundleManifest.Returns(new SceneAssetBundleManifest(FAKE_AB_PATH, "v" + (SceneAssetBundleManifest.ASSET_BUNDLE_VERSION_REQUIRES_HASH - 1), Array.Empty<string>(), "hash", "04_10_2024"));

            var intent = GetAssetBundleIntention.FromHash(typeof(GameObject), "abcd", permittedSources: AssetSource.WEB);
            Entity e = world.Create(intent, new StreamableLoadingState());

            system.Update(0);

            Assert.That(world.TryGet(e, out StreamableLoadingResult<AssetBundleData> result), Is.True);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Asset, Is.Null);
            Assert.That(result.Exception, Is.TypeOf<ArgumentException>().Or.InnerException.TypeOf<ArgumentException>());
        }
        
        [Test]
        public void FailIfAbsentInManifestNewHash()
        {
            LogAssert.ignoreFailingMessages = true;

            sceneData.AssetBundleManifest.Returns(new SceneAssetBundleManifest(FAKE_AB_PATH, "v" + (SceneAssetBundleManifest.ASSET_BUNDLE_VERSION_REQUIRES_HASH), Array.Empty<string>(), "hash", "04_10_2024"));

            var intent = GetAssetBundleIntention.FromHash(typeof(GameObject), "abcd", permittedSources: AssetSource.WEB);
            Entity e = world.Create(intent, new StreamableLoadingState());

            system.Update(0);

            Assert.That(world.TryGet(e, out StreamableLoadingResult<AssetBundleData> result), Is.True);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Asset, Is.Null);
            Assert.That(result.Exception, Is.TypeOf<ArgumentException>().Or.InnerException.TypeOf<ArgumentException>());
        }
    }
}
