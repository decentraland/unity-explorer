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
using Utility;

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

        private static readonly URLDomain FAKE_AB_PATH = URLDomain.FromString("http://www.fakepath.com/v1/");

        [Test]
        public void ResolveHashFromName()
        {
            sceneData.TryGetHash("TEST", out Arg.Any<string>())
                     .Returns(c =>
                      {
                          c[1] = "abcd";
                          return true;
                      });

            var intent = GetAssetBundleIntention.FromName("TEST", permittedSources: AssetSource.EMBEDDED);

            Entity e = world.Create(intent, new StreamableLoadingState());

            system.Update(0);

            intent = world.Get<GetAssetBundleIntention>(e);

            Assert.That(intent.CommonArguments.Attempts, Is.EqualTo(1));
            Assert.That(intent.CommonArguments.CurrentSource, Is.EqualTo(AssetSource.EMBEDDED));
            Assert.That(intent.CommonArguments.URL.Value, Is.EqualTo(path.Value + "abcd" + PlatformUtils.GetPlatform()).Or.EqualTo(path.Value + "abcd".GetHashCode()));
        }

        [Test]
        public void LoadFromEmbeddedFirst()
        {
            var intent = GetAssetBundleIntention.FromHash(typeof(GameObject), "TEST", permittedSources: AssetSource.EMBEDDED | AssetSource.WEB);

            Entity e = world.Create(intent, new StreamableLoadingState());

            system.Update(0);

            intent = world.Get<GetAssetBundleIntention>(e);

            Assert.That(intent.CommonArguments.Attempts, Is.EqualTo(1));
            Assert.That(intent.CommonArguments.CurrentSource, Is.EqualTo(AssetSource.EMBEDDED));
            Assert.That(intent.CommonArguments.URL, Is.EqualTo(path + "TEST"));
        }

        [Test]
        public void LoadFromWeb()
        {
            sceneData.AssetBundleManifest.Returns(new SceneAssetBundleManifest(FAKE_AB_PATH, "200", new[] { "abcd" } ));

            var intent = GetAssetBundleIntention.FromHash(typeof(GameObject), "abcd", permittedSources: AssetSource.WEB);
            Entity e = world.Create(intent, new StreamableLoadingState());
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
            sceneData.AssetBundleManifest.Returns(new SceneAssetBundleManifest(FAKE_AB_PATH, null, Array.Empty<string>()));

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
