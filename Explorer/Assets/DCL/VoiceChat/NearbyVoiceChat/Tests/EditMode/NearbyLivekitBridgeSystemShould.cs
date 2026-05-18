using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Profiles;
using DCL.VoiceChat.Nearby.Audio;
using DCL.VoiceChat.Nearby.Systems;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;
using Object = UnityEngine.Object;

namespace DCL.VoiceChat.Nearby.Tests
{
    /// <summary>
    /// Documents the contract of <see cref="NearbyLivekitBridgeSystem"/> after the resolver-dedup collapse:
    /// every avatar's <see cref="NearbyAudioStreamerComponent.CurrentSid"/> reflects the registry's single
    /// active pick via <see cref="INearbyAudioStreamRegistry.GetActiveSid"/>;
    /// <see cref="IsActivelySpeakingTag"/> reflects the registry's active-speaker snapshot. Pure pull-mirror;
    /// pass-through under listening gate.
    /// Invariant I1: <c>IsActivelySpeakingTag ⊆ NearbyAudioStreamerComponent</c>.
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
            // Default: no active sid, no participants indexed, no active speakers. ReturnsForAnyArgs so the
            // default stub does not count as a received call for short-circuit assertions in DoesNotRevisit...
            registry.GetActiveSid(Arg.Any<string>()).ReturnsForAnyArgs((string?)null);
            registry.HasAudioStream(Arg.Any<string>()).ReturnsForAnyArgs(false);
            registry.IsActiveSpeaker(Arg.Any<string>()).ReturnsForAnyArgs(false);

            system = new NearbyLivekitBridgeSystem(world, registry);
        }

        protected override void OnTearDown()
        {
            foreach (GameObject go in gameObjects)
                if (go != null) Object.DestroyImmediate(go);

            gameObjects.Clear();
            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        // ── AddStreaming query ──────────────────────────────────────

        [Test]
        public void AddStreamingAttachesComponentWhenResolverReturnsActiveSid()
        {
            const string WALLET = "wallet-a";
            const string SID = "sid-1";
            Entity e = CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, SID);

            system.Update(0);

            Assert.That(world.Has<NearbyAudioStreamerComponent>(e), Is.True);
            Assert.That(world.Get<NearbyAudioStreamerComponent>(e).CurrentSid, Is.EqualTo(SID));
        }

        [Test]
        public void AddStreamingSkipsAvatarWithDeleteEntityIntention()
        {
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            world.Add<DeleteEntityIntention>(e);
            StubStreaming(WALLET, "sid-1");

            system.Update(0);

            Assert.That(world.Has<NearbyAudioStreamerComponent>(e), Is.False);
        }

        [Test]
        public void AddStreamingSkipsAvatarAlreadyHavingComponent()
        {
            // Filter [None<NearbyAudioStreamerComponent>] must prevent the AddStreaming query from
            // re-attaching a fresh component on an avatar that already carries one. RefreshStreaming
            // is the single place that mutates an existing component.
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            world.Add(e, new NearbyAudioStreamerComponent("sid-pre"));
            StubStreaming(WALLET, "sid-other");

            system.Update(0);

            // Either Update kept it or refreshed CurrentSid in place; either way, AddStreaming must NOT
            // have piled on a second component (would throw on Arch).
            Assert.That(world.Has<NearbyAudioStreamerComponent>(e), Is.True);
        }

        [Test]
        public void AddStreamingSkipsAvatarWithEmptyWalletId()
        {
            // Avatar with empty UserId. Poison the empty-string slot with a non-null active sid so an
            // impl which fails to short-circuit on empty walletId would mistakenly attach the component.
            var avatarGo = CreateTrackedGameObject("Avatar_empty");
            AvatarBase avatarBase = avatarGo.AddComponent<AvatarBase>();
            var anchorGo = CreateTrackedGameObject("HeadAnchor_empty");
            anchorGo.transform.SetParent(avatarGo.transform);
            HEAD_ANCHOR_FIELD.SetValue(avatarBase, anchorGo.transform);

            registry.GetActiveSid("").Returns("sid-poison");
            registry.HasAudioStream("").Returns(true);

            Entity e = world.Create(new Profile("", "", new Avatar()), avatarBase);

            system.Update(0);

            Assert.That(world.Has<NearbyAudioStreamerComponent>(e), Is.False);
        }

        [Test]
        public void AddStreamingWaitsDuringAllZerosWindow()
        {
            // All-zeros window: registry has the identity but the resolver has not picked a sid yet
            // (no candidate has emitted a frame). The bridge must not attach a component — wait for
            // the next tick, the resolver self-heals on the first frame.
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            registry.GetActiveSid(WALLET).Returns((string?)null);
            registry.HasAudioStream(WALLET).Returns(true);

            system.Update(0);

            Assert.That(world.Has<NearbyAudioStreamerComponent>(e), Is.False,
                "all-zeros window must defer attachment until the resolver picks a sid");
        }

        // ── RefreshStreaming query ───────────────────────────────────

        [Test]
        public void RefreshStreamingNoOpWhenResolverStable()
        {
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, "sid-1");

            system.Update(0);
            Assert.That(world.Has<NearbyAudioStreamerComponent>(e), Is.True);

            // Two more ticks; resolver picks the same sid each time.
            system.Update(0);
            system.Update(0);

            Assert.That(world.Get<NearbyAudioStreamerComponent>(e).CurrentSid, Is.EqualTo("sid-1"),
                "stable resolver → CurrentSid must remain unchanged across ticks");
        }

        [Test]
        public void T6_AvatarGetsComponentWhenResolverFlipsFromNullToSid()
        {
            // Test 6 from the spec: avatar starts with no component (resolver returns null — either
            // unknown identity or all-zeros window). On the next tick the resolver picks sid-42; the
            // bridge must attach NearbyAudioStreamerComponent(CurrentSid = "sid-42").
            const string WALLET = "wallet-a";
            const string SID_42 = "sid-42";
            Entity e = CreateAvatarEntity(WALLET);

            // Resolver: null on tick 1 (no pick yet), wallet not indexed.
            registry.GetActiveSid(WALLET).Returns((string?)null);
            registry.HasAudioStream(WALLET).Returns(false);

            system.Update(0);
            Assert.That(world.Has<NearbyAudioStreamerComponent>(e), Is.False, "precondition: no component while resolver returns null");

            // Resolver: sid-42 on tick 2.
            registry.GetActiveSid(WALLET).Returns(SID_42);
            registry.HasAudioStream(WALLET).Returns(true);

            system.Update(0);

            Assert.That(world.Has<NearbyAudioStreamerComponent>(e), Is.True, "resolver flip null → sid-42 must attach the component");
            Assert.That(world.Get<NearbyAudioStreamerComponent>(e).CurrentSid, Is.EqualTo(SID_42));
        }

        [Test]
        public void T7_AvatarCurrentSidMutatesInPlaceWhenResolverFlipsSids()
        {
            // Test 7 from the spec: avatar already carries the component with CurrentSid = sid-42; on a
            // later tick the resolver picks sid-7 instead (the previous winner was demoted by a fresher
            // candidate). Bridge must ref-mutate CurrentSid without dropping/re-adding the component —
            // structural change here would invalidate the speaking/audible cascade invariants.
            const string WALLET = "wallet-a";
            const string SID_42 = "sid-42";
            const string SID_7 = "sid-7";
            Entity e = CreateAvatarEntity(WALLET);

            StubStreaming(WALLET, SID_42);
            system.Update(0);
            Assert.That(world.Get<NearbyAudioStreamerComponent>(e).CurrentSid, Is.EqualTo(SID_42), "precondition: CurrentSid = sid-42");

            // Resolver flips winner.
            registry.GetActiveSid(WALLET).Returns(SID_7);
            // HasAudioStream stays true — only the active pick changed.

            system.Update(0);

            Assert.That(world.Has<NearbyAudioStreamerComponent>(e), Is.True, "component must persist across the flip (structural stability)");
            Assert.That(world.Get<NearbyAudioStreamerComponent>(e).CurrentSid, Is.EqualTo(SID_7), "resolver flip sid-42 → sid-7 must ref-mutate CurrentSid");
        }

        [Test]
        public void T8_AvatarLosesComponentWhenHasAudioStreamGoesFalseButNotDuringAllZerosWindow()
        {
            // Test 8 from the spec — combined two-step assertion.
            //   Step A — all-zeros window: HasAudioStream=true, GetActiveSid=null → wait, do NOT drop.
            //   Step B — identity gone: HasAudioStream=false, GetActiveSid=null → drop with cascade.
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);

            StubStreaming(WALLET, "sid-1");
            registry.IsActiveSpeaker(WALLET).Returns(true);

            system.Update(0);
            Assert.That(world.Has<NearbyAudioStreamerComponent>(e), Is.True, "precondition: component attached");
            Assert.That(world.Has<IsActivelySpeakingTag>(e), Is.True, "precondition: speaking tag set");
            world.Add(e, new InAudibleRangeTag { IsSuspended = false });

            // Step A — resolver enters all-zeros window (identity still indexed, no candidate emitting).
            registry.GetActiveSid(WALLET).Returns((string?)null);
            registry.HasAudioStream(WALLET).Returns(true);

            system.Update(0);

            Assert.That(world.Has<NearbyAudioStreamerComponent>(e), Is.True,
                "all-zeros window (HasAudioStream && active=null) must NOT drop the component");
            Assert.That(world.Has<IsActivelySpeakingTag>(e), Is.True, "speaking tag must persist through the all-zeros window");
            Assert.That(world.Has<InAudibleRangeTag>(e), Is.True, "audible-range tag must persist through the all-zeros window");

            // Step B — identity disappears entirely.
            registry.HasAudioStream(WALLET).Returns(false);

            system.Update(0);

            Assert.That(world.Has<NearbyAudioStreamerComponent>(e), Is.False, "HasAudioStream=false must drop the component");
            Assert.That(world.Has<IsActivelySpeakingTag>(e), Is.False, "cascade must drop speaking tag (invariant I1)");
            Assert.That(world.Has<InAudibleRangeTag>(e), Is.False, "cascade must drop audible-range tag");
        }

        [Test]
        public void BulkCascadeRemovalProcessesEveryEntityWithinSameTick()
        {
            // Regression for the iteration-safety hazard called out in code review:
            // when registry drops streams for multiple avatars simultaneously, the cleanup
            // path must process every entity in a single tick — no skips even though each
            // World.Remove<NearbyAudioStreamerComponent> triggers an archetype move during
            // query iteration. Pins the contract that motivated splitting Update into
            // RefreshStreaming (ref-mutation only) + RemoveStreaming (structural changes,
            // no outstanding ref).
            const int COUNT = 8;
            var entities = new Entity[COUNT];
            var wallets = new string[COUNT];

            for (int i = 0; i < COUNT; i++)
            {
                wallets[i] = $"wallet-{i}";
                entities[i] = CreateAvatarEntity(wallets[i]);
                StubStreaming(wallets[i], $"sid-{i}");
                registry.IsActiveSpeaker(wallets[i]).Returns(true);
            }

            system.Update(0); // reach steady state: every avatar has streamer + speaking tag

            for (int i = 0; i < COUNT; i++)
            {
                Assert.That(world.Has<NearbyAudioStreamerComponent>(entities[i]), Is.True, $"precondition: entity {i} streamer attached");
                Assert.That(world.Has<IsActivelySpeakingTag>(entities[i]), Is.True, $"precondition: entity {i} speaking tag set");
                world.Add(entities[i], new InAudibleRangeTag { IsSuspended = false });
            }

            // Streams disappear for every wallet on the same tick — identity fully gone (not the all-zeros window).
            for (int i = 0; i < COUNT; i++)
            {
                registry.GetActiveSid(wallets[i]).Returns((string?)null);
                registry.HasAudioStream(wallets[i]).Returns(false);
            }

            system.Update(0);

            for (int i = 0; i < COUNT; i++)
            {
                Assert.That(world.Has<NearbyAudioStreamerComponent>(entities[i]), Is.False, $"entity {i}: streamer must be stripped");
                Assert.That(world.Has<IsActivelySpeakingTag>(entities[i]), Is.False, $"entity {i}: speaking tag must cascade off");
                Assert.That(world.Has<InAudibleRangeTag>(entities[i]), Is.False, $"entity {i}: audible-range tag must cascade off");
            }
        }

        [Test]
        public void DoesNotRevisitEntityAfterStructuralChangeInSameQuery()
        {
            // The filter-trick: AddStreaming's [None<NearbyAudioStreamerComponent>] excludes the entity
            // from its own iterator after `World.Add` migrates it; RefreshStreaming and RemoveStreaming's
            // [All<NearbyAudioStreamerComponent>] symmetrically catch it. Verified by call count —
            // AddStreaming reads the resolver once, RefreshStreaming once (refresh path), RemoveStreaming
            // reads HasAudioStream once (the steady-state guard, returns early since identity is indexed).
            const string WALLET = "wallet-a";
            CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, "sid-1");
            registry.ClearReceivedCalls();

            system.Update(0);

            // AddStreaming + RefreshStreaming each call GetActiveSid once.
            registry.Received(2).GetActiveSid(WALLET);
            // RemoveStreaming guards via HasAudioStream — exactly one call (identity is indexed → early-return).
            registry.Received(1).HasAudioStream(WALLET);
        }

        // ── Speaking / cascade ──────────────────────────────────────

        [Test]
        public void AppliesSpeakingTagWhenRegistryReportsActiveSpeakerWithStreamingComponent()
        {
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, "sid-1");
            registry.IsActiveSpeaker(WALLET).Returns(true);

            system.Update(0);

            Assert.That(world.Has<NearbyAudioStreamerComponent>(e), Is.True);
            Assert.That(world.Has<IsActivelySpeakingTag>(e), Is.True);
        }

        [Test]
        public void DoesNotApplySpeakingTagToAvatarWithoutStreamingComponent()
        {
            // Pins invariant I1: AddSpeaking's [All<NearbyAudioStreamerComponent>] filter must prevent
            // the speaking tag from ever materializing on a non-streaming avatar.
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            registry.IsActiveSpeaker(WALLET).Returns(true);
            // No StubStreaming — resolver reports no audio for this walletId.

            system.Update(0);

            Assert.That(world.Has<NearbyAudioStreamerComponent>(e), Is.False);
            Assert.That(world.Has<IsActivelySpeakingTag>(e), Is.False,
                "speaking tag must require NearbyAudioStreamerComponent (invariant I1)");
        }

        [Test]
        public void DoesNotChurnSpeakingTagWhenStreamingPersists()
        {
            // Regression guard: an unconditional speaking-cascade in Refresh/Remove would drop
            // IsActivelySpeakingTag every tick, then AddSpeaking would re-add it on the same tick —
            // costing a structural-change pair per active speaker per tick. Steady-state contract:
            // both tags settled, only RemoveSpeaking re-checks IsActiveSpeaker. Measure call count.
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
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, "sid-1");
            registry.IsActiveSpeaker(WALLET).Returns(true);

            system.Update(0);
            Assert.That(world.Has<IsActivelySpeakingTag>(e), Is.True, "precondition: speaking tag set");

            registry.IsActiveSpeaker(WALLET).Returns(false);

            system.Update(0);

            Assert.That(world.Has<NearbyAudioStreamerComponent>(e), Is.True,
                "stream is unchanged — NearbyAudioStreamerComponent must persist");
            Assert.That(world.Has<IsActivelySpeakingTag>(e), Is.False);
        }

        [Test]
        public void IdempotentWhenStateUnchangedAcrossTicks()
        {
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, "sid-1");
            registry.IsActiveSpeaker(WALLET).Returns(true);

            system.Update(0);
            system.Update(0);
            system.Update(0);

            Assert.That(world.Has<NearbyAudioStreamerComponent>(e), Is.True);
            Assert.That(world.Has<IsActivelySpeakingTag>(e), Is.True);
        }

        [Test]
        public void AppliesComponentRegardlessOfListeningGateState()
        {
            // The marker system has no reference to NearbyVoiceChatStateModel — never coupled to
            // listening-gate policy. Pinned architecturally: ctor takes only (world, registry).
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, "sid-1");

            system.Update(0);

            Assert.That(world.Has<NearbyAudioStreamerComponent>(e), Is.True,
                "component is pass-through; consumers (Binding/Cleanup/Nametag) own the listening gate");
        }

        // ── Helpers ─────────────────────────────────────────────────

        /// <summary>
        /// Pins both halves of the resolver contract: GetActiveSid returns the given sid AND
        /// HasAudioStream is true. Mirrors a steady-state participant — Bridge's RemoveStreaming
        /// guard ([HasAudioStream==false] is the drop trigger) treats this identity as live.
        /// </summary>
        private string StubStreaming(string walletId, string sid)
        {
            registry.GetActiveSid(walletId).Returns(sid);
            registry.HasAudioStream(walletId).Returns(true);
            return sid;
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
