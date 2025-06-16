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
            Assert.That(intent.CommonArguments.URL.OriginalString, Is.EqualTo(path + "TEST"));
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
            Assert.That(intent.CommonArguments.URL.OriginalString, Is.EqualTo($"http://www.fakepath.com/{version}/abcd"));
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
            Assert.That(intent.CommonArguments.URL.OriginalString, Is.EqualTo($"http://www.fakepath.com/{version}/hash/abcd"));
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

        /*[Test]
        public void ReturnSameCacheValuesForDifferentVersions()
        {
            //First, we simulate creation of a scene and the resolving of one asset budnle
            string version = "v" + SceneAssetBundleManifest.ASSET_BUNDLE_VERSION_REQUIRES_HASH;
            sceneData.AssetBundleManifest.Returns(new SceneAssetBundleManifest(FAKE_AB_PATH, version, new[] { "abcd" }, "scene_hash_1", "04_10_2024"));
            var intent = GetAssetBundleIntention.FromHash(typeof(GameObject), "abcd", permittedSources: AssetSource.WEB);
            Entity entity1 = world.Create(intent, new StreamableLoadingState());
            system.Update(0);
            intent = world.Get<GetAssetBundleIntention>(entity1);
            string firstStandardURL = intent.CommonArguments.URL;
            world.Destroy(entity1);

            //Now, we simulate another scene, that has a different asset bundle version
            version = "v" + (SceneAssetBundleManifest.ASSET_BUNDLE_VERSION_REQUIRES_HASH + 1);
            sceneData.AssetBundleManifest.Returns(new SceneAssetBundleManifest(FAKE_AB_PATH, version, new[] { "abcd" }, "scene_hash_1", "04_10_2024"));
            intent = GetAssetBundleIntention.FromHash(typeof(GameObject), "abcd", permittedSources: AssetSource.WEB);
            Entity entity2 = world.Create(intent, new StreamableLoadingState());
            system.Update(0);
            intent = world.Get<GetAssetBundleIntention>(entity2);
            string secondStandardURL = intent.CommonArguments.URL;

            Assert.AreNotEqual(firstStandardURL, secondStandardURL);
        }

        [Test]
        public void ReturnSameCacheValuesForScenes()
        {
            //First, we simulate creation of a scene and the resolving of one asset budnle
            string version = "v" + SceneAssetBundleManifest.ASSET_BUNDLE_VERSION_REQUIRES_HASH;
            sceneData.AssetBundleManifest.Returns(new SceneAssetBundleManifest(FAKE_AB_PATH, version, new[] { "abcd" }, "scene_hash_1", "04_10_2024"));
            var intent = GetAssetBundleIntention.FromHash(typeof(GameObject), "abcd", permittedSources: AssetSource.WEB);
            Entity entity1 = world.Create(intent, new StreamableLoadingState());
            system.Update(0);
            intent = world.Get<GetAssetBundleIntention>(entity1);
            string firstStandardURL = intent.CommonArguments.URL;
            string firstCacheableHash = intent.CommonArguments.GetCacheableURL();
            world.Destroy(entity1);

            //Now, we simulate another scene, that has a differente scene hash but same version
            sceneData.AssetBundleManifest.Returns(new SceneAssetBundleManifest(FAKE_AB_PATH, version, new[] { "abcd" }, "scene_hash_2", "04_10_2024"));
            intent = GetAssetBundleIntention.FromHash(typeof(GameObject), "abcd", permittedSources: AssetSource.WEB);
            Entity entity2 = world.Create(intent, new StreamableLoadingState());
            system.Update(0);
            intent = world.Get<GetAssetBundleIntention>(entity2);
            string secondStandardURL = intent.CommonArguments.URL;
            string secondCacheableHash = intent.CommonArguments.GetCacheableURL();

            Assert.AreNotEqual(firstStandardURL, secondStandardURL);
            Assert.AreEqual(firstCacheableHash, secondCacheableHash);
        }*/
    }
}
