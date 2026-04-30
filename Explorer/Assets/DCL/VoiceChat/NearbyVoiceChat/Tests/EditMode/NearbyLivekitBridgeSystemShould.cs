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
    /// Documents the contract of <see cref="NearbyLivekitBridgeSystem"/>: every avatar's
    /// <see cref="StreamingAudioComponent.SidsSnapshot"/> reflects the registry's COW sid array
    /// (reference-equal); <see cref="IsActivelySpeakingTag"/> reflects the registry's active-speaker
    /// snapshot. Pure pull-mirror; pass-through under listening gate.
    /// Invariant I1: <c>IsActivelySpeakingTag ⊆ StreamingAudioComponent</c>.
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
            registry.GetAudioSidsArray(Arg.Any<string>()).Returns((string[]?)null);
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

        // ── AddStreaming query ──────────────────────────────────────

        [Test]
        public void AddStreamingAttachesComponentWhenRegistryHasSids()
        {
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            string[] sids = StubStreaming(WALLET, "sid-1");

            system.Update(0);

            Assert.That(world.Has<StreamingAudioComponent>(e), Is.True);
            Assert.That(ReferenceEquals(world.Get<StreamingAudioComponent>(e).SidsSnapshot, sids), Is.True,
                "SidsSnapshot must be reference-equal to the registry's COW array");
        }

        [Test]
        public void AddStreamingSkipsAvatarWithDeleteEntityIntention()
        {
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            world.Add<DeleteEntityIntention>(e);
            StubStreaming(WALLET, "sid-1");

            system.Update(0);

            Assert.That(world.Has<StreamingAudioComponent>(e), Is.False);
        }

        [Test]
        public void AddStreamingSkipsAvatarAlreadyHavingComponent()
        {
            // Filter [None<StreamingAudioComponent>] must prevent the AddStreaming query from
            // re-attaching a fresh component on an avatar that already carries one. UpdateStreaming
            // is the single place that mutates an existing component.
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            string[] preexisting = { "sid-pre" };
            world.Add(e, new StreamingAudioComponent(preexisting));
            StubStreaming(WALLET, "sid-other");

            system.Update(0);

            // Either Update kept it (preexisting reference) or refreshed it to the registry's array;
            // either way, AddStreaming must NOT have piled on a second component.
            Assert.That(world.Has<StreamingAudioComponent>(e), Is.True);
        }

        [Test]
        public void AddStreamingSkipsAvatarWithEmptyWalletId()
        {
            // Avatar with empty UserId. Poison the empty-string slot with a non-null sids array
            // so an impl which fails to short-circuit on empty walletId would mistakenly attach
            // the component. The empty-walletId guard is the observable behavior.
            var avatarGo = CreateTrackedGameObject("Avatar_empty");
            AvatarBase avatarBase = avatarGo.AddComponent<AvatarBase>();
            var anchorGo = CreateTrackedGameObject("HeadAnchor_empty");
            anchorGo.transform.SetParent(avatarGo.transform);
            HEAD_ANCHOR_FIELD.SetValue(avatarBase, anchorGo.transform);

            registry.GetAudioSidsArray("").Returns(new[] { "sid-poison" });

            Entity e = world.Create(new Profile("", "", new Avatar()), avatarBase);

            system.Update(0);

            Assert.That(world.Has<StreamingAudioComponent>(e), Is.False);
        }

        // ── UpdateStreaming query ───────────────────────────────────

        [Test]
        public void UpdateStreamingNoOpWhenReferenceUnchanged()
        {
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            string[] sids = StubStreaming(WALLET, "sid-1");

            system.Update(0);
            Assert.That(world.Has<StreamingAudioComponent>(e), Is.True);

            // Two more ticks; registry returns the same array reference each time.
            system.Update(0);
            system.Update(0);

            Assert.That(ReferenceEquals(world.Get<StreamingAudioComponent>(e).SidsSnapshot, sids), Is.True,
                "stable registry → SidsSnapshot reference must remain unchanged across ticks");
        }

        [Test]
        public void UpdateStreamingRefreshesReferenceWhenRegistryArrayChanged()
        {
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            string[] firstRef = StubStreaming(WALLET, "sid-1");

            system.Update(0);
            Assert.That(ReferenceEquals(world.Get<StreamingAudioComponent>(e).SidsSnapshot, firstRef), Is.True);

            // Registry publishes a NEW array (content changed, reference changed) — simulate the
            // post-OnTrackSubscribed COW snapshot. Bridge must observe the new reference and refresh
            // the entity's snapshot.
            string[] secondRef = { "sid-1", "sid-2" };
            registry.GetAudioSidsArray(WALLET).Returns(secondRef);

            system.Update(0);

            Assert.That(ReferenceEquals(world.Get<StreamingAudioComponent>(e).SidsSnapshot, secondRef), Is.True,
                "new registry reference → SidsSnapshot must adopt the new reference");
        }

        [Test]
        public void UpdateStreamingCascadesFullRemovalWhenRegistryReturnsNull()
        {
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, "sid-1");
            registry.IsActiveSpeaker(WALLET).Returns(true);

            system.Update(0);
            Assert.That(world.Has<StreamingAudioComponent>(e), Is.True, "precondition: component attached");
            Assert.That(world.Has<IsActivelySpeakingTag>(e), Is.True, "precondition: speaking tag set");

            // Seed the dependent marker AudibleRangeSystem would have placed (suspended-flag rides inside it).
            world.Add(e, new InAudibleRangeTag { IsSuspended = true });

            // Stream disappears.
            registry.GetAudioSidsArray(WALLET).Returns((string[]?)null);

            system.Update(0);

            Assert.That(world.Has<StreamingAudioComponent>(e), Is.False);
            Assert.That(world.Has<IsActivelySpeakingTag>(e), Is.False, "cascade must drop speaking (invariant I1)");
            Assert.That(world.Has<InAudibleRangeTag>(e), Is.False, "cascade must drop audible-range (suspended flag is enforced as subset by type)");
        }

        [Test]
        public void DoesNotRevisitEntityAfterStructuralChangeInSameQuery()
        {
            // The filter-trick: AddStreaming's [None<StreamingAudioComponent>] excludes the entity
            // from its own iterator after `World.Add` migrates it; UpdateStreaming's
            // [All<StreamingAudioComponent>] symmetrically catches it. Verified by call count —
            // AddStreaming reads the registry once, UpdateStreaming reads it once.
            const string WALLET = "wallet-a";
            CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, "sid-1");
            registry.ClearReceivedCalls();

            system.Update(0);

            registry.Received(2).GetAudioSidsArray(WALLET);
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

            Assert.That(world.Has<StreamingAudioComponent>(e), Is.True);
            Assert.That(world.Has<IsActivelySpeakingTag>(e), Is.True);
        }

        [Test]
        public void DoesNotApplySpeakingTagToAvatarWithoutStreamingComponent()
        {
            // Pins invariant I1: AddSpeaking's [All<StreamingAudioComponent>] filter must prevent
            // the speaking tag from ever materializing on a non-streaming avatar.
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            registry.IsActiveSpeaker(WALLET).Returns(true);
            // No StubStreaming — registry reports no audio for this walletId.

            system.Update(0);

            Assert.That(world.Has<StreamingAudioComponent>(e), Is.False);
            Assert.That(world.Has<IsActivelySpeakingTag>(e), Is.False,
                "speaking tag must require StreamingAudioComponent (invariant I1)");
        }

        [Test]
        public void DoesNotChurnSpeakingTagWhenStreamingPersists()
        {
            // Regression guard: an unconditional speaking-cascade in UpdateStreaming would drop
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

            Assert.That(world.Has<StreamingAudioComponent>(e), Is.True,
                "stream is unchanged — StreamingAudioComponent must persist");
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

            Assert.That(world.Has<StreamingAudioComponent>(e), Is.True);
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

            Assert.That(world.Has<StreamingAudioComponent>(e), Is.True,
                "component is pass-through; consumers (Binding/Cleanup/Nametag) own the listening gate");
        }

        // ── Helpers ─────────────────────────────────────────────────

        private string[] StubStreaming(string walletId, params string[] sids)
        {
            registry.GetAudioSidsArray(walletId).Returns(sids);
            return sids;
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
