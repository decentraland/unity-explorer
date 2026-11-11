using Arch.Core;
using DCL.AvatarRendering.Loading.Components;
using DCL.ECSComponents;
using DCL.Ipfs;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.GLTF;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;

using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;
using GltfPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.GLTF.GLTFData, ECS.StreamableLoading.GLTF.GetGLTFIntention>;

namespace DCL.AvatarRendering.Emotes.Tests
{
    public class GetSceneEmoteFromLocalSceneIntentionShould
    {
        private World world = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void MaskFullBody_RequestsLegacyClips()
        {
            GetGLTFIntention gltfIntention = CreatePromiseAndGetGLTFIntention(AvatarEmoteMask.AemFullBody);

            Assert.IsFalse(gltfIntention.MecanimAnimationClips,
                "FullBody scene emotes must load as Legacy animations to avoid GLTFast SetCurve failures in builds.");
        }

        [Test]
        public void MaskUpperBody_AlsoRequestsLegacyClips()
        {
            GetGLTFIntention gltfIntention = CreatePromiseAndGetGLTFIntention(AvatarEmoteMask.AemUpperBody);

            Assert.IsFalse(gltfIntention.MecanimAnimationClips,
                "Local scene emotes always load as Legacy regardless of mask — PR #8559 unified the path under Legacy-based mask layering for LSD.");
        }

        [Test]
        public void EqualityDistinguishesMask()
        {
            ISceneData sceneData = Substitute.For<ISceneData>();

            var fullBody = new GetSceneEmoteFromLocalSceneIntention(sceneData, "emote.glb", "hash", BodyShape.MALE, loop: false, AvatarEmoteMask.AemFullBody);
            var upperBody = new GetSceneEmoteFromLocalSceneIntention(sceneData, "emote.glb", "hash", BodyShape.MALE, loop: false, AvatarEmoteMask.AemUpperBody);

            Assert.IsFalse(fullBody.Equals(upperBody),
                "Intentions with identical hash/path/bodyShape but different masks must not be equal to prevent promise-cache cross-contamination.");
        }

        [Test]
        public void LoadBundleWithRawGltfFallbackWhenLocalManifestIsAvailable()
        {
            var intention = new GetSceneEmoteFromLocalSceneIntention(CreateSceneData(AssetBundleManifestVersion.CreateManualManifest()),
                "emote.glb", "hash", BodyShape.MALE, loop: false, AvatarEmoteMask.AemFullBody);

            intention.CreateAndAddPromiseToWorld(world, PartitionComponent.TOP_PRIORITY, null, useLocalAssetBundles: true, Substitute.For<IEmote>());

            Assert.AreEqual(0, world.CountEntities(new QueryDescription().WithAll<GltfPromise>()));
            var fallbacks = new List<SceneEmoteRawGltfFallback>();
            world.Query(new QueryDescription().WithAll<AssetBundlePromise, SceneEmoteRawGltfFallback>(), (ref SceneEmoteRawGltfFallback f) => fallbacks.Add(f));
            Assert.AreEqual(1, fallbacks.Count);
            Assert.AreEqual("emote.glb", fallbacks[0].EmotePath);
            Assert.AreEqual("hash", fallbacks[0].EmoteHash);
            Assert.AreSame(intention.CancellationTokenSource, fallbacks[0].CancellationTokenSource);
        }

        [Test]
        public void LoadRawGltfWhenLocalManifestFailed() =>
            AssertLoadsRawGltf(AssetBundleManifestVersion.FAILED, useLocalAssetBundles: true);

        [Test]
        public void LoadRawGltfWhenLocalManifestIsMissing() =>
            AssertLoadsRawGltf(null, useLocalAssetBundles: true);

        [Test]
        public void LoadRawGltfWhenLocalAssetBundlesAreOff() =>
            AssertLoadsRawGltf(AssetBundleManifestVersion.CreateManualManifest(), useLocalAssetBundles: false);

        private void AssertLoadsRawGltf(AssetBundleManifestVersion? manifest, bool useLocalAssetBundles)
        {
            var intention = new GetSceneEmoteFromLocalSceneIntention(CreateSceneData(manifest), "emote.glb", "hash", BodyShape.MALE, loop: false, AvatarEmoteMask.AemFullBody);

            intention.CreateAndAddPromiseToWorld(world, PartitionComponent.TOP_PRIORITY, null, useLocalAssetBundles, Substitute.For<IEmote>());

            Assert.AreEqual(0, world.CountEntities(new QueryDescription().WithAll<AssetBundlePromise>()));
            var promises = new List<GltfPromise>();
            world.Query(new QueryDescription().WithAll<GltfPromise>(), (ref GltfPromise p) => promises.Add(p));
            Assert.AreEqual(1, promises.Count);
            Assert.AreEqual("emote.glb", promises[0].LoadingIntention.Name);
            Assert.AreEqual("hash", promises[0].LoadingIntention.Hash);
            Assert.AreSame(intention.CancellationTokenSource, promises[0].LoadingIntention.CancellationTokenSource);
        }

        private static ISceneData CreateSceneData(AssetBundleManifestVersion? manifest)
        {
            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.SceneEntityDefinition.Returns(new SceneEntityDefinition { id = "scene-id", assetBundleManifestVersion = manifest });
            return sceneData;
        }

        private GetGLTFIntention CreatePromiseAndGetGLTFIntention(AvatarEmoteMask mask)
        {
            IEmote emote = Substitute.For<IEmote>();

            var intention = new GetSceneEmoteFromLocalSceneIntention(CreateSceneData(null), "emote.glb", "hash", BodyShape.MALE, loop: false, mask);

            intention.CreateAndAddPromiseToWorld(world, PartitionComponent.TOP_PRIORITY, null, useLocalAssetBundles: false, emote);

            var query = new QueryDescription().WithAll<GltfPromise>();
            GltfPromise? capturedPromise = null;

            world.Query(query, (ref GltfPromise p) => capturedPromise = p);

            Assert.IsNotNull(capturedPromise, "CreateAndAddPromiseToWorld should create an entity carrying a GltfPromise.");

            return capturedPromise!.Value.LoadingIntention;
        }
    }
}
