using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
using DCL.Profiles;
using DCL.VoiceChat.Nearby.MutePersistence;
using DCL.VoiceChat.Nearby.Systems;
using ECS.TestSuite;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.VoiceChat.Nearby.Tests
{
    /// <summary>
    /// Documents the Nearby Audio Position System behavior — strict read+drive, no lifecycle responsibility:
    ///
    /// - Reads avatar position via <see cref="NearbyAudioSourceComponent.AvatarEntity"/>.
    /// - Drives <see cref="LivekitAudioSource"/> transform + spatial angles each frame.
    /// - Reprojects source position relative to the player head.
    ///
    /// Structural changes for audio entities are owned exclusively by <see cref="NearbyAudioCleanupSystem"/>.
    /// </summary>
    public class NearbyAudioPositionSystemShould : UnitySystemTestBase<NearbyAudioPositionSystem>
    {
        private const string PARTICIPANT_A = "wallet-alice";

        private static readonly FieldInfo HEAD_ANCHOR_FIELD =
            typeof(AvatarBase).GetField("<HeadAnchorPoint>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private readonly List<GameObject> gameObjects = new (8);

        private Camera camera = null!;
        private Transform playerHead = null!;
        private INearbyMuteCache muteCache = null!;
        private NearbyMuteService muteService = null!;
        private NearbyListenerState listenerState = null!;

        [SetUp]
        public void SetUp()
        {
            EcsTestsUtils.SetUpFeaturesRegistry();

            var cameraGo = CreateTrackedGameObject("TestCamera");
            camera = cameraGo.AddComponent<Camera>();

            // Default head anchor collocated with the camera; tests that need a non-collocated head mutate via SeedListener.
            playerHead = CreateTrackedGameObject("TestPlayerHead").transform;
            playerHead.position = camera.transform.position;

            muteCache = Substitute.For<INearbyMuteCache>();
            muteService = new NearbyMuteService(muteCache, Substitute.For<INearbyMuteRepository>());

            listenerState = new NearbyListenerState();
            listenerState.BindListener(playerHead, camera.transform);

            system = new NearbyAudioPositionSystem(world, muteService, listenerState);
            system.Initialize();
        }

        // Mirror what NearbyAudibleRangeSystem would drive into the head transform this tick.
        private void SeedListener(Vector3 playerHeadPos)
        {
            playerHead.position = playerHeadPos;
        }

        protected override void OnTearDown()
        {
            foreach (GameObject go in gameObjects)
                if (go != null) Object.DestroyImmediate(go);

            gameObjects.Clear();

            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        // ── Position Sync ───────────────────────────────────────────

        [Test]
        public void SyncsAudioSourceToRemoteHeadWhenListenerCollocatedWithPlayerHead()
        {
            // Degenerate reprojection case: listener and player head share a position, so
            // sourcePos = listener + (remoteHead - playerHead) collapses to remoteHead.
            // This is approximately the FirstPerson case in production.
            Vector3 avatarPos = new Vector3(10, 0, 5);
            Vector3 headPos = avatarPos + new Vector3(0, 1.6f, 0);
            Entity avatarEntity = CreateAvatarEntity(PARTICIPANT_A, avatarPos, headPos);
            LivekitAudioSource audioSource = CreateLivekitAudioSource();
            CreateAudioEntity(PARTICIPANT_A, "sid-1", avatarEntity, audioSource);

            system.Update(0);

            Assert.That(audioSource.transform.position.x, Is.EqualTo(headPos.x).Within(0.01f));
            Assert.That(audioSource.transform.position.y, Is.EqualTo(headPos.y).Within(0.01f));
            Assert.That(audioSource.transform.position.z, Is.EqualTo(headPos.z).Within(0.01f));
        }

        [Test]
        public void ReprojectsAudioSourcePositionRelativeToPlayerHead()
        {
            // General case (covers ThirdPerson and the FP↔TP transition):
            // listener is offset from the player's head, so the source is reprojected to
            // listener + (remoteHead - playerHead) — pan/gain stay head-relative.
            var playerHeadPos = new Vector3(0, 1.6f, 0);
            SeedListener(playerHeadPos);

            Vector3 avatarPos = new Vector3(8, 0, 4);
            Vector3 remoteHead = avatarPos + new Vector3(0, 1.6f, 0);
            Entity avatarEntity = CreateAvatarEntity(PARTICIPANT_A, avatarPos, remoteHead);
            LivekitAudioSource audioSource = CreateLivekitAudioSource();
            CreateAudioEntity(PARTICIPANT_A, "sid-1", avatarEntity, audioSource);

            system.Update(0);

            Vector3 listenerPos = camera.transform.position;
            Vector3 expected = listenerPos + (remoteHead - playerHeadPos);

            Assert.That(audioSource.transform.position.x, Is.EqualTo(expected.x).Within(0.01f));
            Assert.That(audioSource.transform.position.y, Is.EqualTo(expected.y).Within(0.01f));
            Assert.That(audioSource.transform.position.z, Is.EqualTo(expected.z).Within(0.01f));
        }

        // ── Mute Per-Frame ──────────────────────────────────────────

        [Test]
        public void MutedIdentityKeepsMutedAudioSourceFromBinding()
        {
            // Pessimistic init: binding starts AudioSource.mute=true and the component's LastAppliedMute=true.
            // For an already-muted user, the first tick reads the cache once (Version mismatch from 0→1) but
            // skips the AudioSource.mute interop because the cached value already matches LastAppliedMute.
            Entity avatarEntity = CreateAvatarEntity(PARTICIPANT_A, Vector3.zero, new Vector3(0, 1.6f, 0));
            LivekitAudioSource lkSource = CreateLivekitAudioSource();
            lkSource.AudioSource.mute = true;   // matches the binding contract
            CreateAudioEntity(PARTICIPANT_A, "sid-1", avatarEntity, lkSource);

            muteCache.Version.Returns(1u);
            muteCache.IsMuted(PARTICIPANT_A).Returns(true);

            system.Update(0);

            Assert.That(lkSource.AudioSource.mute, Is.True);
        }

        [Test]
        public void MuteToggleIsSelfHealing()
        {
            Entity avatarEntity = CreateAvatarEntity(PARTICIPANT_A, Vector3.zero, new Vector3(0, 1.6f, 0));
            LivekitAudioSource lkSource = CreateLivekitAudioSource();
            lkSource.AudioSource.mute = true;
            CreateAudioEntity(PARTICIPANT_A, "sid-1", avatarEntity, lkSource);

            // Production flow: NearbyMuteCache bumps Version on every effective mutation.
            // Simulate three distinct cache mutations across the three ticks below.
            muteCache.Version.Returns(1u, 2u, 3u);

            // muted → tick → muted (matches pessimistic init, no write but state stays correct)
            muteCache.IsMuted(PARTICIPANT_A).Returns(true);
            system.Update(0);
            Assert.That(lkSource.AudioSource.mute, Is.True, "Expected mute=true after first tick");

            // flip to unmuted → tick → unmuted (write triggered by value diff against LastAppliedMute)
            muteCache.IsMuted(PARTICIPANT_A).Returns(false);
            system.Update(0);
            Assert.That(lkSource.AudioSource.mute, Is.False, "Expected mute=false after toggle to unmuted");

            // flip back to muted → tick → muted (per-toggle, gated by Version bump)
            muteCache.IsMuted(PARTICIPANT_A).Returns(true);
            system.Update(0);
            Assert.That(lkSource.AudioSource.mute, Is.True, "Expected mute=true after toggle back");
        }

        [Test]
        public void UnmutedIdentityHasUnmutedAudioSource()
        {
            Entity avatarEntity = CreateAvatarEntity(PARTICIPANT_A, Vector3.zero, new Vector3(0, 1.6f, 0));
            LivekitAudioSource lkSource = CreateLivekitAudioSource();
            // Start muted as the binding system would — first successful tick must release it.
            lkSource.AudioSource.mute = true;
            CreateAudioEntity(PARTICIPANT_A, "sid-1", avatarEntity, lkSource);

            muteCache.Version.Returns(1u);
            muteCache.IsMuted(PARTICIPANT_A).Returns(false);

            system.Update(0);

            Assert.That(lkSource.AudioSource.mute, Is.False);
        }

        // ── A1: inactive-state handling (suspend / out-of-range) ────

        [Test]
        public void StopsAudioSourceWhenAvatarIsSuspended()
        {
            Entity avatarEntity = CreateAvatarEntity(PARTICIPANT_A, Vector3.zero, new Vector3(0, 1.6f, 0));
            world.Get<InAudibleRangeTag>(avatarEntity).IsSuspended = true;

            LivekitAudioSource lkSource = CreateLivekitAudioSource();
            lkSource.AudioSource.Play();
            CreateAudioEntity(PARTICIPANT_A, "sid-1", avatarEntity, lkSource);

            system.Update(0);

            Assert.That(lkSource.AudioSource.isPlaying, Is.False);
        }

        [Test]
        public void StopsAudioSourceWhenAvatarIsOutOfRange()
        {
            // One-frame transient: AudibleRangeMarker (AvatarGroup) drops InAudibleRangeTag on
            // outer-out crossing; Cleanup (CleanUpGroup) dooms the audio entity later in the same
            // frame. PositionSystem runs in between and must not invoke the spatial pipeline on
            // a doomed entity — the !Has<InAudibleRangeTag> clause in `inactive` covers it.
            Entity avatarEntity = CreateAvatarEntity(PARTICIPANT_A, Vector3.zero, new Vector3(0, 1.6f, 0));
            world.Remove<InAudibleRangeTag>(avatarEntity);

            LivekitAudioSource lkSource = CreateLivekitAudioSource();
            lkSource.AudioSource.Play();
            CreateAudioEntity(PARTICIPANT_A, "sid-1", avatarEntity, lkSource);

            system.Update(0);

            Assert.That(lkSource.AudioSource.isPlaying, Is.False);
        }

        [Test]
        public void ResumesPlaybackWhenAvatarTransitionsFromSuspendedToActive()
        {
            // Diff-write contract: the Stop/Play flip is gated by a transition of the per-entity
            // active/inactive bit (LastInactive). Drive the source through suspend→active; the
            // second tick must observe the diff and call Play again.
            Entity avatarEntity = CreateAvatarEntity(PARTICIPANT_A, Vector3.zero, new Vector3(0, 1.6f, 0));
            world.Get<InAudibleRangeTag>(avatarEntity).IsSuspended = true;

            LivekitAudioSource lkSource = CreateLivekitAudioSource();
            CreateAudioEntity(PARTICIPANT_A, "sid-1", avatarEntity, lkSource);

            // Tick 1: suspended → LastInactive flips false→true, source stopped.
            system.Update(0);
            Assume.That(lkSource.AudioSource.isPlaying, Is.False);

            // Avatar enters the active band.
            world.Get<InAudibleRangeTag>(avatarEntity).IsSuspended = false;

            // Tick 2: active → LastInactive flips true→false, source resumed.
            system.Update(0);

            Assert.That(lkSource.AudioSource.isPlaying, Is.True);
        }

        [Test]
        public void SkipsSpatialPipelineWhenInactive()
        {
            // Suspended avatar — spatial pipeline must early-return: position not written,
            // mute not touched. Pin a sentinel position on the source and check it's untouched.
            Entity avatarEntity = CreateAvatarEntity(PARTICIPANT_A, Vector3.zero, new Vector3(0, 1.6f, 0));
            world.Get<InAudibleRangeTag>(avatarEntity).IsSuspended = true;

            LivekitAudioSource lkSource = CreateLivekitAudioSource();
            Vector3 sentinelPos = new (123.4f, 56.7f, 89.1f);
            lkSource.transform.position = sentinelPos;
            lkSource.AudioSource.mute = true; // would be flipped to false by the mute branch if it ran
            CreateAudioEntity(PARTICIPANT_A, "sid-1", avatarEntity, lkSource);

            muteCache.IsMuted(PARTICIPANT_A).Returns(false);

            system.Update(0);

            Assert.That(lkSource.transform.position, Is.EqualTo(sentinelPos),
                "spatial pipeline must not write transform.position when inactive");
            Assert.That(lkSource.AudioSource.mute, Is.True,
                "spatial pipeline must not touch AudioSource.mute when inactive");
            Assert.That(muteCache.ReceivedCalls(), Is.Empty,
                "mute service must not be queried when the spatial pipeline is skipped");
        }

        [Test]
        public void AppliesIdempotentWritesWhenStateUnchanged()
        {
            Entity avatarEntity = CreateAvatarEntity(PARTICIPANT_A, Vector3.zero, new Vector3(0, 1.6f, 0));
            world.Get<InAudibleRangeTag>(avatarEntity).IsSuspended = true;

            LivekitAudioSource lkSource = CreateLivekitAudioSource();
            CreateAudioEntity(PARTICIPANT_A, "sid-1", avatarEntity, lkSource);

            Assert.DoesNotThrow(() =>
            {
                system.Update(0);
                system.Update(0);
            });

            Assert.That(lkSource.AudioSource.isPlaying, Is.False);
        }

        // ── Diff-write (B3): mute version + transform delta ─────────

        [Test]
        public void SkipMuteInteropWhenCacheVersionUnchanged()
        {
            // After the pessimistic-init tick has settled state, a subsequent tick with the same Version
            // must skip both the IsMuted lookup and the AudioSource.mute write.
            Entity avatarEntity = CreateAvatarEntity(PARTICIPANT_A, Vector3.zero, new Vector3(0, 1.6f, 0));
            LivekitAudioSource lkSource = CreateLivekitAudioSource();
            lkSource.AudioSource.mute = true;
            CreateAudioEntity(PARTICIPANT_A, "sid-1", avatarEntity, lkSource);

            muteCache.Version.Returns(1u);
            muteCache.IsMuted(PARTICIPANT_A).Returns(false);

            // Tick 1 absorbs pessimistic init: writes mute=false (IsMuted=false != LastApplied=true).
            system.Update(0);
            Assume.That(lkSource.AudioSource.mute, Is.False, "Sanity: first tick wrote mute=false");

            // Sentinel: if tick 2 performs any write, it would overwrite this back to false.
            lkSource.AudioSource.mute = true;
            muteCache.ClearReceivedCalls();

            // Tick 2 with same Version: no lookup, no write.
            system.Update(0);

            Assert.That(lkSource.AudioSource.mute, Is.True, "Sentinel survives — interop write skipped");
            muteCache.DidNotReceive().IsMuted(Arg.Any<string>());
        }

        [Test]
        public void SkipMuteInteropEvenWhenVersionBumpedIfValueUnchanged()
        {
            // Version moves (some other wallet was toggled), but THIS entity's mute state is unchanged.
            // Lookup happens (Version mismatched), but the AudioSource.mute interop is gated by the value diff.
            Entity avatarEntity = CreateAvatarEntity(PARTICIPANT_A, Vector3.zero, new Vector3(0, 1.6f, 0));
            LivekitAudioSource lkSource = CreateLivekitAudioSource();
            lkSource.AudioSource.mute = true;
            CreateAudioEntity(PARTICIPANT_A, "sid-1", avatarEntity, lkSource);

            muteCache.Version.Returns(1u, 2u);
            muteCache.IsMuted(PARTICIPANT_A).Returns(true); // muted across both ticks

            system.Update(0); // version=1, IsMuted=true matches LastApplied=true → no write

            // Sentinel
            lkSource.AudioSource.mute = false;

            system.Update(0); // version=2, IsMuted=true still matches LastApplied=true → no write

            Assert.That(lkSource.AudioSource.mute, Is.False, "Sentinel survives — value-diff gate skipped the write");
        }

        [Test]
        public void SkipTransformWriteWhenPositionDeltaBelowEpsilon()
        {
            // 5 cm shift → sqrMagnitude = 0.0025 < POSITION_EPSILON_SQR (0.01). Transform write skipped.
            Vector3 head = new Vector3(1, 2, 3);
            Entity avatarEntity = CreateAvatarEntity(PARTICIPANT_A, Vector3.zero, head);
            LivekitAudioSource lkSource = CreateLivekitAudioSource();
            CreateAudioEntity(PARTICIPANT_A, "sid-1", avatarEntity, lkSource);

            muteCache.Version.Returns(1u);

            system.Update(0); // pessimistic-init: writes head pos
            Assume.That(lkSource.transform.position.x, Is.EqualTo(head.x).Within(0.001f));

            // Move head anchor by 5 cm — below threshold.
            AvatarBase avatarBase = world.Get<AvatarBase>(avatarEntity);
            avatarBase.HeadAnchorPoint.position = head + new Vector3(0.05f, 0, 0);

            system.Update(0);

            // Last-written position retained because delta was below epsilon.
            Assert.That(lkSource.transform.position.x, Is.EqualTo(head.x).Within(0.001f));
            Assert.That(lkSource.transform.position.y, Is.EqualTo(head.y).Within(0.001f));
            Assert.That(lkSource.transform.position.z, Is.EqualTo(head.z).Within(0.001f));
        }

        [Test]
        public void WriteTransformWhenPositionDeltaExceedsEpsilon()
        {
            // 50 cm shift → sqrMagnitude = 0.25 > POSITION_EPSILON_SQR. Transform write applied.
            Vector3 head = new Vector3(1, 2, 3);
            Entity avatarEntity = CreateAvatarEntity(PARTICIPANT_A, Vector3.zero, head);
            LivekitAudioSource lkSource = CreateLivekitAudioSource();
            CreateAudioEntity(PARTICIPANT_A, "sid-1", avatarEntity, lkSource);

            muteCache.Version.Returns(1u);

            system.Update(0); // pessimistic-init: writes head pos

            Vector3 newHead = head + new Vector3(0.5f, 0, 0);
            AvatarBase avatarBase = world.Get<AvatarBase>(avatarEntity);
            avatarBase.HeadAnchorPoint.position = newHead;

            system.Update(0);

            Assert.That(lkSource.transform.position.x, Is.EqualTo(newHead.x).Within(0.001f));
            Assert.That(lkSource.transform.position.y, Is.EqualTo(newHead.y).Within(0.001f));
            Assert.That(lkSource.transform.position.z, Is.EqualTo(newHead.z).Within(0.001f));
        }

        // ── Helpers ─────────────────────────────────────────────────

        private GameObject CreateTrackedGameObject(string name)
        {
            var go = new GameObject(name);
            gameObjects.Add(go);
            return go;
        }

        private Entity CreateAvatarEntity(string walletId, Vector3 avatarPos, Vector3 headAnchorPos)
        {
            var avatarGo = CreateTrackedGameObject($"Remote_{walletId}");
            avatarGo.transform.position = avatarPos;

            AvatarBase avatarBase = avatarGo.AddComponent<AvatarBase>();
            var headAnchorGo = CreateTrackedGameObject($"HeadAnchor_{walletId}");
            headAnchorGo.transform.SetParent(avatarGo.transform, worldPositionStays: false);
            headAnchorGo.transform.position = headAnchorPos;
            HEAD_ANCHOR_FIELD.SetValue(avatarBase, headAnchorGo.transform);

            // A1: PositionSystem early-returns the spatial pipeline unless InAudibleRangeTag is
            // present with IsSuspended=false. Default seed is "active in range" so the
            // existing per-frame contract tests (position sync, mute, reprojection) keep working
            // without per-test scaffolding. Inactive-state tests opt into the suspend/out-of-range
            // state explicitly (mutate IsSuspended or remove the tag).
            Entity entity = world.Create(
                new Profile(walletId, walletId, new Avatar()),
                avatarBase,
                new CharacterTransform(avatarGo.transform));

            world.Add<InAudibleRangeTag>(entity);
            return entity;
        }

        private Entity CreateAudioEntity(string identity, string sid, Entity avatarEntity, LivekitAudioSource source)
        {
            var key = new StreamKey(identity, sid);
            return world.Create(new NearbyAudioSourceComponent(key, avatarEntity, source));
        }

        private LivekitAudioSource CreateLivekitAudioSource()
        {
            LivekitAudioSource source = LivekitAudioSource.New();
            gameObjects.Add(source.gameObject);
            return source;
        }
    }
}
