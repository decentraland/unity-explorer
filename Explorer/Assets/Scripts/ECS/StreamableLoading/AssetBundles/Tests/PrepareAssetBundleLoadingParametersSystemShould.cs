using Arch.Core;
using AssetManagement;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles.Tests
{
    [TestFixture]
    public class PrepareAssetBundleLoadingParametersSystemShould : UnitySystemTestBase<PrepareAssetBundleLoadingParametersSystem>
    {
        private ISceneData sceneData;
        private string path;

        [SetUp]
        public void SetUp()
        {
            path = $"file://{Application.dataPath}" + "/../TestResources/AssetBundles/";
            sceneData = Substitute.For<ISceneData>();
            system = new PrepareAssetBundleLoadingParametersSystem(world, sceneData, path);
        }

        [Test]
        public void LoadFromEmbeddedFirst()
        {
            var intent = new GetAssetBundleIntention("TEST", permittedSources: AssetSource.EMBEDDED | AssetSource.WEB);

            Entity e = world.Create(intent);

            system.Update(0);

            intent = world.Get<GetAssetBundleIntention>(e);

            Assert.That(intent.CommonArguments.Attempts, Is.EqualTo(1));
            Assert.That(intent.CommonArguments.CurrentSource, Is.EqualTo(AssetSource.EMBEDDED));
            Assert.That(intent.CommonArguments.URL, Is.EqualTo(path + "TEST"));
        }

        [Test]
        public void LoadFromWeb()
        {
            sceneData.AssetBundleManifest.Returns(new SceneAssetBundleManifest("http://www.fakepath.com/v1/", new SceneAbDto { files = new[] { "abcd" }, version = "200" }));

            var intent = new GetAssetBundleIntention("abcd", permittedSources: AssetSource.WEB);
            Entity e = world.Create(intent);
            system.Update(0);

            intent = world.Get<GetAssetBundleIntention>(e);

            Assert.That(intent.CommonArguments.Attempts, Is.EqualTo(StreamableLoadingDefaults.ATTEMPTS_COUNT));
            Assert.That(intent.CommonArguments.CurrentSource, Is.EqualTo(AssetSource.WEB));
            Assert.That(intent.CommonArguments.URL, Is.EqualTo("http://www.fakepath.com/v1/200/abcd"));
            Assert.That(intent.cacheHash, Is.Not.Null);
        }

        [Test]
        public void FailIfAbsentInManifest()
        {
            sceneData.AssetBundleManifest.Returns(new SceneAssetBundleManifest("http://www.fakepath.com/v1/", new SceneAbDto { files = Array.Empty<string>() }));

            var intent = new GetAssetBundleIntention("abcd", permittedSources: AssetSource.WEB);
            Entity e = world.Create(intent);

            system.Update(0);

            Assert.That(world.TryGet(e, out StreamableLoadingResult<AssetBundle> result), Is.True);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Asset, Is.Null);
            Assert.That(result.Exception, Is.TypeOf<ArgumentException>());
        }
    }
}
