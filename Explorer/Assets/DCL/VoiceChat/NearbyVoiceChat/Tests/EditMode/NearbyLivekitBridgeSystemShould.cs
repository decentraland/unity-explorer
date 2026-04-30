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
    /// Documents the contract of <see cref="NearbyLivekitBridgeSystem"/>: archetype state on
    /// the avatar entity (<see cref="StreamingAudioComponent"/>, <see cref="IsActivelySpeakingTag"/>)
    /// is reconciled once per tick from the current <see cref="INearbyAudioStreamRegistry"/>
    /// snapshot. Pure pull-mirror; pass-through under listening gate (consumers own the policy).
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
            // the slot they care about. Bridge ONLY consumes GetAudioSidsArray (string[]?) on the
            // streaming hot path — the ReadOnlySpan-returning GetAudioSids is never called by Bridge,
            // so the NSubstitute proxy never has to materialize a ref-struct return.
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

        // ── Streaming component — Add ───────────────────────────────

        [Test]
        public void AttachesStreamingComponentWhenRegistryReportsWalletId()
        {
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, "sid-1");

            system.Update(0);

            Assert.That(world.Has<StreamingAudioComponent>(e), Is.True);
        }

        [Test]
        public void AddStreamingAttachesComponentWithReferenceEqualSnapshot()
        {
            // The component carries the registry's COW reference verbatim — Bridge does NOT
            // copy the array. Reference identity is the freshness signal Update relies on.
            const string WALLET = "wallet-a";
            string[] registryArr = { "sid-1" };
            Entity e = CreateAvatarEntity(WALLET);
            registry.GetAudioSidsArray(WALLET).Returns(registryArr);

            system.Update(0);

            ref StreamingAudioComponent comp = ref world.Get<StreamingAudioComponent>(e);
            Assert.That(ReferenceEquals(comp.SidsSnapshot, registryArr), Is.True,
                "SidsSnapshot must be reference-equal to the registry array");
        }

        [Test]
        public void RemovesStreamingComponentWhenWalletIdDisappearsFromRegistry()
        {
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, "sid-1");
            system.Update(0); // component attached

            registry.GetAudioSidsArray(WALLET).Returns((string[]?)null);

            system.Update(0);

            Assert.That(world.Has<StreamingAudioComponent>(e), Is.False);
        }

        [Test]
        public void KeepsStreamingComponentWhenOneOfNSidsUnsubscribes()
        {
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);

            string[] before = { "sid-1", "sid-2" };
            registry.GetAudioSidsArray(WALLET).Returns(before);

            system.Update(0);
            Assert.That(world.Has<StreamingAudioComponent>(e), Is.True, "precondition: component attached on first tick");

            // One sid unsubscribes → registry returns a fresh COW reference with the survivor.
            string[] after = { "sid-1" };
            registry.GetAudioSidsArray(WALLET).Returns(after);

            system.Update(0);

            Assert.That(world.Has<StreamingAudioComponent>(e), Is.True,
                "component must persist while at least one sid remains for the walletId");
            ref StreamingAudioComponent comp = ref world.Get<StreamingAudioComponent>(e);
            Assert.That(ReferenceEquals(comp.SidsSnapshot, after), Is.True,
                "SidsSnapshot must refresh to the new COW reference");
        }

        [Test]
        public void UpdateStreamingNoOpWhenReferenceUnchanged()
        {
            // Reference-equality is the freshness check. Stable registry → SidsSnapshot reference
            // is invariant across ticks; Bridge does NOT replace the field.
            const string WALLET = "wallet-a";
            string[] stable = { "sid-1" };
            Entity e = CreateAvatarEntity(WALLET);
            registry.GetAudioSidsArray(WALLET).Returns(stable);

            system.Update(0);
            ref StreamingAudioComponent first = ref world.Get<StreamingAudioComponent>(e);
            string[] firstRef = first.SidsSnapshot;

            system.Update(0);
            ref StreamingAudioComponent second = ref world.Get<StreamingAudioComponent>(e);

            Assert.That(ReferenceEquals(second.SidsSnapshot, firstRef), Is.True,
                "stable registry → reference invariant → no write to SidsSnapshot");
        }

        [Test]
        public void UpdateStreamingRefreshesReferenceWhenRegistryArrayChanged()
        {
            const string WALLET = "wallet-a";
            string[] arr1 = { "sid-1" };
            Entity e = CreateAvatarEntity(WALLET);
            registry.GetAudioSidsArray(WALLET).Returns(arr1);

            system.Update(0);

            // Add a sid → registry returns a fresh COW reference.
            string[] arr2 = { "sid-1", "sid-2" };
            registry.GetAudioSidsArray(WALLET).Returns(arr2);

            system.Update(0);

            ref StreamingAudioComponent comp = ref world.Get<StreamingAudioComponent>(e);
            Assert.That(ReferenceEquals(comp.SidsSnapshot, arr2), Is.True,
                "Bridge must observe the new reference and write it into the component");
        }

        [Test]
        public void UpdateStreamingCascadesFullRemovalWhenRegistryReturnsNull()
        {
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, "sid-1");

            system.Update(0);
            Assert.That(world.Has<StreamingAudioComponent>(e), Is.True, "precondition: component attached");

            // Pre-seed the dependent tags so cascade has something to drop.
            world.Add<InAudibleRangeTag>(e);
            world.Add<IsSuspendedTag>(e);
            registry.IsActiveSpeaker(WALLET).Returns(true);
            // Re-tick so the speaking tag also lands before stream disappears.
            registry.GetAudioSidsArray(WALLET).Returns(new[] { "sid-1" });
            system.Update(0);
            Assert.That(world.Has<IsActivelySpeakingTag>(e), Is.True, "precondition: speaking tag set");

            // Stream disappears.
            registry.GetAudioSidsArray(WALLET).Returns((string[]?)null);

            system.Update(0);

            Assert.That(world.Has<StreamingAudioComponent>(e), Is.False);
            Assert.That(world.Has<IsActivelySpeakingTag>(e), Is.False, "cascade must drop speaking tag");
            Assert.That(world.Has<InAudibleRangeTag>(e), Is.False, "cascade must drop audible-range tag");
            Assert.That(world.Has<IsSuspendedTag>(e), Is.False, "cascade must drop suspended tag");
        }

        // ── Edge cases ──────────────────────────────────────────────

        [Test]
        public void DoesNotRevisitEntityAfterStructuralChangeInSameQuery()
        {
            // The filter-trick: AddStreaming's [None<StreamingAudioComponent>] excludes the entity from
            // its own iterator after `World.Add` migrates it; UpdateStreaming's [All<StreamingAudioComponent>]
            // then iterates the entity exactly once. Two registry reads total per tick.
            const string WALLET = "wallet-a";
            CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, "sid-1");
            registry.ClearReceivedCalls();

            system.Update(0);

            registry.Received(2).GetAudioSidsArray(WALLET);
        }

        [Test]
        public void SkipsAvatarWithDeleteEntityIntention()
        {
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            world.Add<DeleteEntityIntention>(e);
            StubStreaming(WALLET, "sid-1");

            system.Update(0);

            Assert.That(world.Has<StreamingAudioComponent>(e), Is.False);
        }

        [Test]
        public void DoesNotAttachStreamingComponentTwice()
        {
            // [None<StreamingAudioComponent>] filter prevents repeated AddStreaming on a tagged avatar.
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, "sid-1");

            system.Update(0);
            Assert.That(world.Has<StreamingAudioComponent>(e), Is.True);

            // Multiple ticks must not throw (would happen if a second Add tried to attach the same component).
            Assert.DoesNotThrow(() => system.Update(0));
            Assert.DoesNotThrow(() => system.Update(0));
        }

        [Test]
        public void SkipsAvatarWithNullOrEmptyWalletId()
        {
            // Empty walletId guard — poison the empty slot with a non-null array; an impl
            // missing the guard would mistakenly attach the component.
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

        [Test]
        public void AppliesTagsRegardlessOfListeningGateState()
        {
            // The Bridge system has no reference to NearbyVoiceChatStateModel — it must
            // never be coupled to listening-gate policy. Constructing with only (world, registry) is the contract.
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, "sid-1");

            system.Update(0);

            Assert.That(world.Has<StreamingAudioComponent>(e), Is.True,
                "Bridge is pass-through; consumers (Binding/Cleanup/Nametag) own the listening gate");
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

        // ── Speaking tag ────────────────────────────────────────────

        [Test]
        public void CascadeRemovesSpeakingTagWhenStreamingComponentIsRemovedSameTick()
        {
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, "sid-1");
            registry.IsActiveSpeaker(WALLET).Returns(true);

            system.Update(0);
            Assert.That(world.Has<StreamingAudioComponent>(e), Is.True, "precondition: streaming component set");
            Assert.That(world.Has<IsActivelySpeakingTag>(e), Is.True, "precondition: speaking tag set");

            registry.GetAudioSidsArray(WALLET).Returns((string[]?)null);

            system.Update(0);

            Assert.That(world.Has<StreamingAudioComponent>(e), Is.False);
            Assert.That(world.Has<IsActivelySpeakingTag>(e), Is.False,
                "cascade must drop speaking when streaming is removed (invariant I1)");
        }

        [Test]
        public void CascadeRemovesAudibleAndSuspendedWhenStreamingComponentIsRemovedSameTick()
        {
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            StubStreaming(WALLET, "sid-1");

            system.Update(0);
            Assert.That(world.Has<StreamingAudioComponent>(e), Is.True, "precondition: streaming component set");

            world.Add<InAudibleRangeTag>(e);
            world.Add<IsSuspendedTag>(e);

            registry.GetAudioSidsArray(WALLET).Returns((string[]?)null);

            system.Update(0);

            Assert.That(world.Has<StreamingAudioComponent>(e), Is.False);
            Assert.That(world.Has<InAudibleRangeTag>(e), Is.False,
                "cascade must drop audible-range when streaming is removed (invariant I2)");
            Assert.That(world.Has<IsSuspendedTag>(e), Is.False,
                "cascade must drop suspended when streaming is removed (invariant I1+I2)");
        }

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
            const string WALLET = "wallet-a";
            Entity e = CreateAvatarEntity(WALLET);
            // No StubStreaming — registry reports no audio for this walletId.
            registry.IsActiveSpeaker(WALLET).Returns(true);

            system.Update(0);

            Assert.That(world.Has<StreamingAudioComponent>(e), Is.False);
            Assert.That(world.Has<IsActivelySpeakingTag>(e), Is.False,
                "speaking tag must require streaming component (invariant I1)");
        }

        [Test]
        public void DoesNotChurnSpeakingTagWhenStreamingPersists()
        {
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
                "stream is unchanged — streaming component must persist");
            Assert.That(world.Has<IsActivelySpeakingTag>(e), Is.False);
        }

        // ── Helpers ─────────────────────────────────────────────────

        private void StubStreaming(string walletId, params string[] sids)
        {
            registry.GetAudioSidsArray(walletId).Returns(sids);
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
