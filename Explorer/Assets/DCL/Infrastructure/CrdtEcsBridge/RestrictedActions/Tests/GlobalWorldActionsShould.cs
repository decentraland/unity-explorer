using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Loading.Components;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Multiplayer.Emotes;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Entity = Arch.Core.Entity;
using DCL.Multiplayer.Profiles.Bunches;
using Utility;

using SceneEmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution, DCL.AvatarRendering.Emotes.GetSceneEmoteFromRealmIntention>;
using LocalSceneEmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution, DCL.AvatarRendering.Emotes.GetSceneEmoteFromLocalSceneIntention>;

namespace CrdtEcsBridge.RestrictedActions.Tests
{
    [TestFixture]
    public class GlobalWorldActionsShould
    {
        private World world;
        private Entity playerEntity;
        private MockEmotesMessageBus mockMessageBus;
        private GlobalWorldActions globalWorldActions;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            playerEntity = world.Create();
            mockMessageBus = new MockEmotesMessageBus();
            globalWorldActions = new GlobalWorldActions(world, playerEntity, mockMessageBus, false, true);

            // camera entity for camera global actions
            world.Create(new CameraComponent());
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void ApplyTeleportIntent()
        {
            var newPosition = new Vector3(10, 0, 10);
            globalWorldActions.MoveAndRotatePlayer(newPosition, null, null);

            Assert.IsTrue(world.Has<PlayerTeleportIntent>(playerEntity));
            var intent = world.Get<PlayerTeleportIntent>(playerEntity);
            Assert.AreEqual(newPosition, intent.Position);
            Assert.IsTrue(intent.IsPositionSet);
        }

        [Test]
        public void ApplyLookAtIntentFromAvatarTarget()
        {
            var playerPos = new Vector3(5, 0, 5);
            var avatarTarget = new Vector3(10, 0, 5);
            var expectedLookAt = playerPos + (avatarTarget - playerPos).normalized;
            expectedLookAt.y = playerPos.y;

            globalWorldActions.MoveAndRotatePlayer(playerPos, null, avatarTarget);

            Assert.IsTrue(world.Has<PlayerLookAtIntent>(playerEntity));
            var intent = world.Get<PlayerLookAtIntent>(playerEntity);
            Assert.AreEqual(expectedLookAt.x, intent.LookAtTarget.x, 0.01f);
            Assert.AreEqual(expectedLookAt.y, intent.LookAtTarget.y, 0.01f);
            Assert.AreEqual(expectedLookAt.z, intent.LookAtTarget.z, 0.01f);
        }

        [Test]
        public void ApplyLookAtIntentFromCameraTargetIfAvatarTargetIsNull()
        {
            var playerPos = new Vector3(5, 0, 5);
            var cameraTarget = new Vector3(5, 0, 10);

            globalWorldActions.MoveAndRotatePlayer(playerPos, cameraTarget, null);

            Assert.IsTrue(world.Has<PlayerLookAtIntent>(playerEntity));
            var intent = world.Get<PlayerLookAtIntent>(playerEntity);
            Assert.AreEqual(cameraTarget, intent.LookAtTarget);
        }

        [Test]
        public void DoNothingIfTargetIsNull()
        {
            var camera = world.CacheCamera();
            globalWorldActions.RotateCamera(null, Vector3.zero);
            Assert.IsFalse(world.Has<CameraLookAtIntent>(camera));
        }

        [Test]
        public void ApplyCameraLookAtIntent()
        {
            var camera = world.CacheCamera();
            var cameraTarget = new Vector3(10, 2, 10);
            var playerPosition = new Vector3(8, 0, 8);

            globalWorldActions.RotateCamera(cameraTarget, playerPosition);

            Assert.IsTrue(world.Has<CameraLookAtIntent>(camera));
            var intent = world.Get<CameraLookAtIntent>(camera);
            Assert.AreEqual(cameraTarget, intent.LookAtTarget);
            Assert.AreEqual(playerPosition, intent.PlayerPosition);
        }

        [Test]
        public void AddIntentAndSendMessageWhenAvatarVisible()
        {
            world.Add(playerEntity, new AvatarShapeComponent { IsVisible = true });
            var emoteUrn = new URN("urn:emote:id");
            var isLooping = true;

            globalWorldActions.TriggerEmote(emoteUrn, isLooping);

            Assert.IsTrue(world.Has<CharacterEmoteIntent>(playerEntity));
            var intent = world.Get<CharacterEmoteIntent>(playerEntity);
            Assert.AreEqual(emoteUrn, intent.EmoteId);
            Assert.IsTrue(intent.Spatial);
            Assert.AreEqual(TriggerSource.SCENE, intent.TriggerSource);

            Assert.AreEqual(1, mockMessageBus.SentEmotes.Count);
            Assert.AreEqual(emoteUrn, mockMessageBus.SentEmotes[0].emoteId);
            Assert.AreEqual(isLooping, mockMessageBus.SentEmotes[0].isLooping);
        }

        [Test]
        public void DoNothingWhenAvatarNotVisible()
        {
            world.Add(playerEntity, new AvatarShapeComponent { IsVisible = false });
            var emoteUrn = new URN("urn:emote:id");

            globalWorldActions.TriggerEmote(emoteUrn, false);

            Assert.IsFalse(world.Has<CharacterEmoteIntent>(playerEntity));
            Assert.AreEqual(0, mockMessageBus.SentEmotes.Count);
        }

        [Test]
        public void CreatePromiseForLocalSceneSceneEmote()
        {
            globalWorldActions = new GlobalWorldActions(world, playerEntity, mockMessageBus, true, false);
            world.Add(playerEntity, new AvatarShapeComponent { BodyShape = BodyShape.MALE, IsVisible = true });

            var mockSceneData = new MockSceneData { SceneShortInfo = new SceneShortInfo(Vector2Int.zero, "localSceneTest") };
            var src = "some_emote.glb";
            var hash = "emote_hash_local";
            var loop = true;

            var promiseOutcomeQuery = new QueryDescription().WithAll<GetSceneEmoteFromLocalSceneIntention>();
            int promiseEntitiesCount = world.CountEntities(in promiseOutcomeQuery);
            Assert.AreEqual(0, promiseEntitiesCount, $"Expected to find 0 promise entity but found {promiseEntitiesCount}.");

            globalWorldActions.TriggerSceneEmoteAsync(mockSceneData, src, hash, loop, CancellationToken.None);

            promiseEntitiesCount = world.CountEntities(in promiseOutcomeQuery);
            Assert.AreEqual(1, promiseEntitiesCount, $"Expected to find 1 promise entity but found {promiseEntitiesCount}.");
        }

        [Test]
        public void NotCreatePromiseForLocalSceneSceneEmoteInvalidSrc()
        {
            globalWorldActions = new GlobalWorldActions(world, playerEntity, mockMessageBus, true, false); // local development, no remote ABs
            world.Add(playerEntity, new AvatarShapeComponent { BodyShape = BodyShape.MALE, IsVisible = true });

            var mockSceneData = new MockSceneData();
            var src = "some_emote_with_invalid_extension.txt"; // Invalid src, doesn't end with _emote.glb
            var hash = "emote_hash_invalid_src";
            var loop = false;

            var promiseOutcomeQuery = new QueryDescription().WithAll<GetSceneEmoteFromLocalSceneIntention>();
            int promiseEntitiesCount = world.CountEntities(in promiseOutcomeQuery);
            Assert.AreEqual(0, promiseEntitiesCount, $"Expected to find 0 promise entity but found {promiseEntitiesCount}.");

            globalWorldActions.TriggerSceneEmoteAsync(mockSceneData, src, hash, loop, CancellationToken.None);

            promiseEntitiesCount = world.CountEntities(in promiseOutcomeQuery);
            Assert.AreEqual(0, promiseEntitiesCount, $"Expected to find 0 promise entity but found {promiseEntitiesCount}.");
        }

        [Test]
        public void CreatePromiseForRealmSceneSceneEmote()
        {
            globalWorldActions = new GlobalWorldActions(world, playerEntity, mockMessageBus, false, true);
            world.Add(playerEntity, new AvatarShapeComponent { BodyShape = BodyShape.FEMALE, IsVisible = true });

            var sceneId = "remoteSceneId";
            var mockSceneData = new MockSceneData { SceneEntityDefinition = new SceneEntityDefinition(sceneId, new SceneMetadata()) };
            mockSceneData.AssetBundleManifest = new SceneAssetBundleManifest(URLDomain.EMPTY, "v1", System.Array.Empty<string>(), "hash", "date");

            var hash = "emote_hash_remote";
            var loop = false;

            var promiseOutcomeQuery = new QueryDescription().WithAll<GetSceneEmoteFromRealmIntention>();
            int promiseEntitiesCount = world.CountEntities(in promiseOutcomeQuery);
            Assert.AreEqual(0, promiseEntitiesCount, $"Expected to find 0 promise entity but found {promiseEntitiesCount}.");

            globalWorldActions.TriggerSceneEmoteAsync(mockSceneData, "ignored_src.glb", hash, loop, CancellationToken.None);

            promiseEntitiesCount = world.CountEntities(in promiseOutcomeQuery);
            Assert.AreEqual(1, promiseEntitiesCount, $"Expected to find 1 promise entity but found {promiseEntitiesCount}.");
        }

        [Test]
        public void ShouldNotTriggerSceneEmoteIfAvatarNotVisible()
        {
            globalWorldActions = new GlobalWorldActions(world, playerEntity, mockMessageBus, false, true);
            world.Add(playerEntity, new AvatarShapeComponent { BodyShape = BodyShape.FEMALE, IsVisible = false });

            var mockSceneData = new MockSceneData { SceneEntityDefinition = new SceneEntityDefinition("sceneInvisibleTest", new SceneMetadata()) };
            var hash = "emote_hash_invisible";

            globalWorldActions.TriggerSceneEmoteAsync(mockSceneData, "any.glb", hash, false, CancellationToken.None);

            Assert.AreEqual(0, mockMessageBus.SentEmotes.Count);
            Assert.IsFalse(world.Has<CharacterEmoteIntent>(playerEntity));
        }

        // Mocks
        private class MockEmotesMessageBus : IEmotesMessageBus
        {
            public List<(URN emoteId, bool isLooping)> SentEmotes = new ();

            public void Send(URN urn, bool loopCyclePassed) // Parameter name from interface
            {
                SentEmotes.Add((urn, loopCyclePassed));
            }

            public OwnedBunch<RemoteEmoteIntention> EmoteIntentions() => throw new System.NotImplementedException();
            public void OnPlayerRemoved(string walletId) => throw new System.NotImplementedException();
            public void SaveForRetry(RemoteEmoteIntention intention) => throw new System.NotImplementedException();
        }

        private class MockSceneData : ISceneData
        {
            public bool SceneLoadingConcluded { get; set; } = true;
            public SceneShortInfo SceneShortInfo { get; set; } = new (Vector2Int.zero, "mockScene");
            public IReadOnlyList<Vector2Int> Parcels { get; set; } = new List<Vector2Int>();
            public ISceneContent SceneContent => new SceneNonHashedContent(URLDomain.FromString("file://mock/"));
            public SceneEntityDefinition SceneEntityDefinition { get; set; } = new ("sceneId", new SceneMetadata());
            public ParcelMathHelper.SceneGeometry Geometry => new (Vector3.zero, new ParcelMathHelper.SceneCircumscribedPlanes(), 0.0f);
            public SceneAssetBundleManifest AssetBundleManifest { get; set; } = SceneAssetBundleManifest.NULL;
            public StaticSceneMessages StaticSceneMessages => StaticSceneMessages.EMPTY;

            public bool HasRequiredPermission(string permission) => true;

            public bool TryGetMainScriptUrl(out URLAddress result)
            {
                result = URLAddress.EMPTY;
                return false;
            }

            public bool TryGetContentUrl(string url, out URLAddress result)
            {
                result = URLAddress.FromString(url); // Simple mock: assume url is the content path
                return true;
            }

            public bool TryGetHash(string name, out string hash)
            {
                hash = name;
                return true;
            }

            public bool TryGetMediaUrl(string url, out URLAddress result)
            {
                result = URLAddress.FromString(url);
                return true;
            }
            public bool TryGetMediaFileHash(string url, out string fileHash) { fileHash = url; return true; }
            public bool IsUrlDomainAllowed(string url) => true;
            public bool IsSdk7() => true;
            public bool IsPortableExperience() => false;
        }
    }
}
