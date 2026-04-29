using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Profiles;
using DCL.VoiceChat.Nearby.Audio;
using DCL.VoiceChat.Nearby.Systems;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;
using Object = UnityEngine.Object;

namespace DCL.VoiceChat.Nearby.Tests
{
    /// <summary>
    /// Documents the contract of <see cref="NearbyLivekitBridgeSystem"/>: zero-field marker components
    /// (<see cref="IsStreamingAudioTag"/>, <see cref="IsActivelySpeakingTag"/>) on the avatar entity
    /// are reconciled once per tick from the current <see cref="INearbyAudioStreamRegistry"/>
    /// snapshot. Pure pull-mirror; pass-through under listening gate (consumers own the policy).
    /// Invariant I1: <c>IsActivelySpeakingTag ⊆ IsStreamingAudioTag</c>.
    /// </summary>
    public class NearbyLivekitBridgeSystemShould : UnitySystemTestBase<NearbyLivekitBridgeSystem>
    {
        private static readonly FieldInfo HEAD_ANCHOR_FIELD =
            typeof(AvatarBase).GetField("<HeadAnchorPoint>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private INearbyAudioStreamRegistry registry;

        private readonly List<GameObject> gameObjects = new (16);

        [SetUp]
        public void SetUp()
        {
            EcsTestsUtils.SetUpFeaturesRegistry();

            registry = Substitute.For<INearbyAudioStreamRegistry>();
            // Default: no streams, no active speakers — explicit so individual tests only override
            // the slot they care about. NSubstitute returns null/false for unstubbed reference/bool
            // returns, but stating the contract makes intent legible.
            registry.GetAudioSids(Arg.Any<string>()).Returns((ConcurrentDictionary<string, byte>?)null);
            registry.IsActiveSpeaker(Arg.Any<string>()).Returns(false);

            system = new NearbyLivekitBridgeSystem(world, registry);
        }

        protected override void OnTearDown()
        {
            foreach (GameObject go in gameObjects)
                if (go != null) Object.DestroyImmediate(go);

            gameObjects.Clear();
            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        // ── Streaming tag — Query A (Add) ────────────────────────────

        [Test]
        public void AppliesStreamingTagWhenRegistryReportsWalletId()
        {
            // Arrange
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, "sid-1");

            // Act
            system.Update(0);

            // Assert
            Assert.That(world.Has<IsStreamingAudioTag>(e), Is.True);
        }

        [Test]
        public void RemovesStreamingTagWhenWalletIdDisappearsFromRegistry()
        {
            // Arrange
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, "sid-1");
            system.Update(0); // tag is now applied

            // walletId drops out of registry
            registry.GetAudioSids(WALLET).Returns((ConcurrentDictionary<string, byte>?)null);

            // Act
            system.Update(0);

            // Assert
            Assert.That(world.Has<IsStreamingAudioTag>(e), Is.False);
        }

        [Test]
        public void KeepsStreamingTagWhenOneOfNSidsUnsubscribes()
        {
            // Arrange — multi-track participant
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            var sids = new ConcurrentDictionary<string, byte>();
            sids.TryAdd("sid-1", 0);
            sids.TryAdd("sid-2", 0);
            registry.GetAudioSids(WALLET).Returns(sids);

            system.Update(0);
            Assert.That(world.Has<IsStreamingAudioTag>(e), Is.True, "precondition: tag applied on first tick");

            // Act — one sid unsubscribes; outer dict still non-null
            sids.TryRemove("sid-2", out _);
            system.Update(0);

            // Assert
            Assert.That(world.Has<IsStreamingAudioTag>(e), Is.True,
                "tag must persist while at least one sid remains for the walletId");
        }

        // ── Edge cases ──────────────────────────────────────────────

        [Test]
        public void DoesNotRevisitEntityAfterStructuralChangeInSameQuery()
        {
            // The filter-trick: Query A's [None<IsStreamingAudioTag>] excludes the entity from
            // its own iterator after `World.Add` migrates it to a new archetype; Query B's
            // [All<IsStreamingAudioTag>] does the symmetric thing on Remove. Verified by
            // call count — Query A reads the registry exactly once, Query B reads it exactly
            // once (and then short-circuits because the stream is still present). If either
            // filter trick were broken the same query would re-enter and the count would balloon.
            const string WALLET = "wallet-a";
            CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, "sid-1");
            registry.ClearReceivedCalls(); // discard NSubstitute setup-side recordings

            system.Update(0);

            registry.Received(2).GetAudioSids(WALLET);
        }

        [Test]
        public void SkipsAvatarWithDeleteEntityIntention()
        {
            // Arrange
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            world.Add<DeleteEntityIntention>(e);
            StubStreaming(WALLET, "sid-1");

            // Act
            system.Update(0);

            // Assert
            Assert.That(world.Has<IsStreamingAudioTag>(e), Is.False);
        }

        [Test]
        public void SkipsAvatarWithNullOrEmptyWalletId()
        {
            // Arrange — avatar with empty UserId. We poison the empty-string slot with a non-null
            // sids dict so that an impl which fails to short-circuit on empty walletId would
            // mistakenly tag the entity. The empty-walletId guard is the observable behavior.
            var avatarGo = CreateTrackedGameObject("Avatar_empty");
            AvatarBase avatarBase = avatarGo.AddComponent<AvatarBase>();
            var anchorGo = CreateTrackedGameObject("HeadAnchor_empty");
            anchorGo.transform.SetParent(avatarGo.transform);
            HEAD_ANCHOR_FIELD.SetValue(avatarBase, anchorGo.transform);

            var poisonSids = new ConcurrentDictionary<string, byte>();
            poisonSids.TryAdd("sid-poison", 0);
            registry.GetAudioSids("").Returns(poisonSids);

            Entity e = world.Create(new Profile("", "", new Avatar()), avatarBase);

            // Act
            system.Update(0);

            // Assert
            Assert.That(world.Has<IsStreamingAudioTag>(e), Is.False);
        }

        [Test]
        public void AppliesTagsRegardlessOfListeningGateState()
        {
            // The marker system has no reference to NearbyVoiceChatStateModel — it must
            // never be coupled to listening-gate policy. This test pins that invariant
            // architecturally: constructing the system with only (world, registry) is the contract.
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, "sid-1");

            system.Update(0);

            Assert.That(world.Has<IsStreamingAudioTag>(e), Is.True,
                "marker is pass-through; consumers (Binding/Cleanup/Nametag) own the listening gate");
        }

        [Test]
        public void IdempotentWhenStateUnchangedAcrossTicks()
        {
            // Arrange — both tags should be present steady-state.
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, "sid-1");
            registry.IsActiveSpeaker(WALLET).Returns(true);

            // Act — multiple ticks with no registry change
            system.Update(0);
            system.Update(0);
            system.Update(0);

            // Assert — tag presence is the observable invariant; archetype-move counting is
            // not part of Arch's public API.
            Assert.That(world.Has<IsStreamingAudioTag>(e), Is.True);
            Assert.That(world.Has<IsActivelySpeakingTag>(e), Is.True);
        }

        // ── Speaking tag — Query C / Query D / cascade ───────────────

        [Test]
        public void CascadeRemovesSpeakingTagWhenStreamingTagIsRemovedSameTick()
        {
            // Arrange — bring the entity into steady state with both tags.
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, "sid-1");
            registry.IsActiveSpeaker(WALLET).Returns(true);

            system.Update(0);
            Assert.That(world.Has<IsStreamingAudioTag>(e), Is.True, "precondition: streaming tag set");
            Assert.That(world.Has<IsActivelySpeakingTag>(e), Is.True, "precondition: speaking tag set");

            // Stream disappears, but the avatar is still listed as an active speaker.
            // Without cascade, invariant I1 (speaking ⊆ streaming) would break.
            registry.GetAudioSids(WALLET).Returns((ConcurrentDictionary<string, byte>?)null);

            // Act
            system.Update(0);

            // Assert — both tags gone in the same tick.
            Assert.That(world.Has<IsStreamingAudioTag>(e), Is.False);
            Assert.That(world.Has<IsActivelySpeakingTag>(e), Is.False,
                "cascade must drop speaking when streaming is removed (invariant I1)");
        }

        [Test]
        public void AppliesSpeakingTagWhenRegistryReportsActiveSpeakerWithStreamingTag()
        {
            // Arrange
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, "sid-1");
            registry.IsActiveSpeaker(WALLET).Returns(true);

            // Act
            system.Update(0);

            // Assert
            Assert.That(world.Has<IsStreamingAudioTag>(e), Is.True);
            Assert.That(world.Has<IsActivelySpeakingTag>(e), Is.True);
        }

        [Test]
        public void DoesNotApplySpeakingTagToAvatarWithoutStreamingTag()
        {
            // Arrange — avatar IS an active speaker but has NO audio stream.
            // This pins invariant I1 architecturally: Query C's [All<IsStreamingAudioTag>] filter
            // must prevent the speaking tag from ever materializing on a non-streaming avatar
            // (covers the local-player case from invariant I2).
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            // No StubStreaming — registry reports no audio for this walletId.
            registry.IsActiveSpeaker(WALLET).Returns(true);

            // Act
            system.Update(0);

            // Assert
            Assert.That(world.Has<IsStreamingAudioTag>(e), Is.False);
            Assert.That(world.Has<IsActivelySpeakingTag>(e), Is.False,
                "speaking tag must require streaming tag (invariant I1)");
        }

        [Test]
        public void DoesNotChurnSpeakingTagWhenStreamingPersists()
        {
            // Regression guard: an unconditional cascade in RemoveStreamingTag would unconditionally
            // drop IsActivelySpeakingTag every tick, then Query C would re-add it on the same tick —
            // costing a structural-change pair per active speaker per tick, with both tags present
            // observably (so a tag-presence assertion alone does not catch it).
            //
            // Steady-state contract: with both tags settled, only Query D needs to re-check
            // IsActiveSpeaker. Query C must not iterate (entity already has IsActivelySpeakingTag).
            // If the cascade misfires, Query C re-iterates the entity → IsActiveSpeaker is called
            // twice per tick instead of once.
            const string WALLET = "wallet-a";
            CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, "sid-1");
            registry.IsActiveSpeaker(WALLET).Returns(true);

            system.Update(0); // reach steady state
            registry.ClearReceivedCalls();

            system.Update(0); // measured tick

            registry.Received(1).IsActiveSpeaker(WALLET);
        }

        [Test]
        public void RemovesSpeakingTagWhenWalletIdDropsFromActiveSpeakersButStillStreaming()
        {
            // Arrange — bring the entity into steady state with both tags.
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, "sid-1");
            registry.IsActiveSpeaker(WALLET).Returns(true);

            system.Update(0);
            Assert.That(world.Has<IsActivelySpeakingTag>(e), Is.True, "precondition: speaking tag set");

            // Active-speaker signal drops, stream remains.
            registry.IsActiveSpeaker(WALLET).Returns(false);

            // Act
            system.Update(0);

            // Assert — only the speaking tag gone; streaming tag stays.
            Assert.That(world.Has<IsStreamingAudioTag>(e), Is.True,
                "stream is unchanged — streaming tag must persist");
            Assert.That(world.Has<IsActivelySpeakingTag>(e), Is.False);
        }

        // ── Helpers ─────────────────────────────────────────────────

        private void StubStreaming(string walletId, params string[] sids)
        {
            var dict = new ConcurrentDictionary<string, byte>();
            foreach (string sid in sids)
                dict.TryAdd(sid, 0);
            registry.GetAudioSids(walletId).Returns(dict);
        }

        private Entity CreateAvatarEntity(string walletId)
        {
            var avatarGo = CreateTrackedGameObject($"Avatar_{walletId}");
            AvatarBase avatarBase = avatarGo.AddComponent<AvatarBase>();
            var headAnchorGo = CreateTrackedGameObject($"HeadAnchor_{walletId}");
            headAnchorGo.transform.SetParent(avatarGo.transform, worldPositionStays: false);
            HEAD_ANCHOR_FIELD.SetValue(avatarBase, headAnchorGo.transform);

            return world.Create(new Profile(walletId, walletId, new Avatar()), avatarBase);
        }

        private GameObject CreateTrackedGameObject(string name)
        {
            var go = new GameObject(name);
            gameObjects.Add(go);
            return go;
        }
    }
}
