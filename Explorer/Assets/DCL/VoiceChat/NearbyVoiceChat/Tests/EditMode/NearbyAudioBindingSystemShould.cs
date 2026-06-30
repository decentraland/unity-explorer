using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Friends.UserBlocking;
using DCL.Profiles;
using DCL.SceneBannedUsers;
using DCL.VoiceChat.Nearby.Audio;
using DCL.VoiceChat.Nearby.Systems;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using NSubstitute;
using NUnit.Framework;
using RichTypes;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;
using Object = UnityEngine.Object;

namespace DCL.VoiceChat.Nearby.Tests
{
    /// <summary>
    /// Documents <see cref="NearbyAudioBindingSystem"/> contract:
    ///
    /// - One audio-source component per avatar entity, created only when the avatar is fully ready
    ///   (Profile + AvatarBase + NearbyAudioStreamerComponent + InAudibleRangeTag, no DeleteEntityIntention).
    /// - Idempotent: re-ticking with no registry changes does not duplicate sources.
    /// - Hot path reads sids from the per-entity <see cref="NearbyAudioStreamerComponent"/>, not the registry.
    /// </summary>
    public class NearbyAudioBindingSystemShould : UnitySystemTestBase<NearbyAudioBindingSystem>
    {
        // Co-located after slice 4: NearbyAudioSourceComponent lives on the avatar entity itself, alongside
        // AvatarBase. The "audio source count" question is therefore "how many avatars carry the component".
        private static readonly QueryDescription AUDIO_SOURCE_QUERY = new QueryDescription().WithAll<NearbyAudioSourceComponent, AvatarBase>();

        private static readonly FieldInfo HEAD_ANCHOR_FIELD =
            typeof(AvatarBase).GetField("<HeadAnchorPoint>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private FakeStreamRegistry registry;
        private IUserBlockingCache userBlockingCache;
        private NearbyVoiceChatStateModel stateModel;

        private FakeNearbyAudioSourceFactory sourceFactory;

        private readonly List<GameObject> gameObjects = new (32);

        [SetUp]
        public void SetUp()
        {
            EcsTestsUtils.SetUpFeaturesRegistry();

            registry = new FakeStreamRegistry();
            userBlockingCache = Substitute.For<IUserBlockingCache>();
            stateModel = new NearbyVoiceChatStateModel(NearbyVoiceChatState.IDLE);
            sourceFactory = new FakeNearbyAudioSourceFactory();

            system = new NearbyAudioBindingSystem(world, registry, userBlockingCache, stateModel, sourceFactory, RoomMetadataCurrentScene.CreateForTest());
        }

        protected override void OnTearDown()
        {
            sourceFactory.DisposeRoot();

            foreach (GameObject go in gameObjects)
                if (go != null) Object.DestroyImmediate(go);

            gameObjects.Clear();

            stateModel.Dispose();

            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        [Test]
        public void SingleAvatarSingleStreamAddsComponentOnAvatar()
        {
            const string WALLET = "wallet-alice";
            Entity avatarEntity = CreateStreamingAvatar(WALLET, "sid-1");
            registry.SeedActiveStream(WALLET, "sid-1");

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(1));

            // Co-location: the component must live on the avatar entity itself (no separate audio entity).
            Assert.That(world.Has<NearbyAudioSourceComponent>(avatarEntity), Is.True,
                "component must be added directly onto the avatar entity (slice-4 co-location)");

            NearbyAudioSourceComponent comp = world.Get<NearbyAudioSourceComponent>(avatarEntity);
            Assert.That(comp.Key, Is.EqualTo(new StreamKey(WALLET, "sid-1")));
            Assert.That(comp.LivekitAudioSource, Is.Not.Null);
        }

        [Test]
        public void AvatarWithoutAvatarBaseIsSkipped()
        {
            const string WALLET = "wallet-alice";
            Entity avatarEntity = world.Create(new Profile(WALLET, WALLET, new Avatar()));
            world.Add(avatarEntity, new NearbyAudioStreamerComponent("sid-1"));
            world.Add<InAudibleRangeTag>(avatarEntity);
            registry.SeedActiveStream(WALLET, "sid-1");

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(0),
                "no AvatarBase = pool exhausted; do not bind audio until the avatar materializes");
        }

        [Test]
        public void IdempotencyDoesNotDuplicateBindings()
        {
            const string WALLET = "wallet-alice";
            CreateStreamingAvatar(WALLET, "sid-1");
            registry.SeedActiveStream(WALLET, "sid-1");

            system.Update(0);
            system.Update(0);
            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(1));
        }

        [Test]
        public void DeleteEntityIntentionAvatarsAreFilteredOut()
        {
            const string WALLET = "wallet-alice";
            Entity avatarEntity = CreateStreamingAvatar(WALLET, "sid-1");
            world.Add<DeleteEntityIntention>(avatarEntity);
            registry.SeedActiveStream(WALLET, "sid-1");

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(0));
        }

        [Test]
        public void BlockedIdentitySkipsCreation()
        {
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            CreateStreamingAvatar(WALLET, SID);
            registry.SeedActiveStream(WALLET, SID);
            userBlockingCache.UserIsBlocked(WALLET).Returns(true);

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(0),
                "blocked identity must not allocate an audio entity");
        }

        [Test]
        public void UnblockReBindsOnNextTick()
        {
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            CreateStreamingAvatar(WALLET, SID);
            registry.SeedActiveStream(WALLET, SID);
            userBlockingCache.UserIsBlocked(WALLET).Returns(true);

            system.Update(0);
            Assert.That(CountAudioEntities(), Is.EqualTo(0), "blocked tick must not allocate");

            userBlockingCache.UserIsBlocked(WALLET).Returns(false);

            system.Update(0);
            Assert.That(CountAudioEntities(), Is.EqualTo(1), "unblock must re-bind on the next tick");
        }

        [Test]
        public void SuppressedStateSkipsCreation()
        {
            const int AVATARS = 5;
            for (int i = 0; i < AVATARS; i++)
            {
                string wallet = $"wallet-{i}";
                CreateStreamingAvatar(wallet, "sid-1");
                registry.SeedActiveStream(wallet, "sid-1");
            }

            stateModel.Suppress(SuppressionReason.CALL);

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(0),
                "SUPPRESSED state must short-circuit creation regardless of registry / avatar readiness");
        }

        [Test]
        public void DisabledStateSkipsCreation()
        {
            const int AVATARS = 5;
            for (int i = 0; i < AVATARS; i++)
            {
                string wallet = $"wallet-{i}";
                CreateStreamingAvatar(wallet, "sid-1");
                registry.SeedActiveStream(wallet, "sid-1");
            }

            stateModel.Disable();

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(0));
        }

        [Test]
        public void ResumeRebindsFromRegistry()
        {
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            CreateStreamingAvatar(WALLET, SID);
            registry.SeedActiveStream(WALLET, SID);
            stateModel.Suppress(SuppressionReason.CALL);

            system.Update(0);
            Assert.That(CountAudioEntities(), Is.EqualTo(0), "suppressed tick must not allocate");

            stateModel.Resume(SuppressionReason.CALL);

            system.Update(0);
            Assert.That(CountAudioEntities(), Is.EqualTo(1), "resume must re-bind from the unchanged component snapshot");
        }

        [Test]
        public void RaceOnSpawnSkipsCreation()
        {
            // The track was unsubscribed between collection (component snapshot) and resolve (GetActiveStream).
            // The binding system must observe Weak<AudioStream>.Null and skip creation rather than spawn a ghost source.
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            CreateStreamingAvatar(WALLET, SID);
            registry.MarkStreamAsUnsubscribed(WALLET, SID);

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(0),
                "Weak<AudioStream>.Null on resolve must not create an audio entity");
        }

        // ── Archetype gate via StreamingAudioComponent / InAudibleRangeTag ─

        [Test]
        public void DoesNotBindAvatarWithoutStreamingComponentEvenIfRegistryHasStream()
        {
            // Binding's query is gated by StreamingAudioComponent. An avatar without the component
            // must be skipped at the chunk-iteration level, even if the registry already has sids.
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            CreateAvatarEntity(WALLET); // intentionally no StreamingAudioComponent
            registry.SeedActiveStream(WALLET, SID);

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(0),
                "absent StreamingAudioComponent must skip the avatar at archetype level");
        }

        [Test]
        public void BindsAvatarWithStreamingComponentAndAudibleRangeTag()
        {
            // Happy path mirror — pinned under the new gate name.
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            Entity avatarEntity = CreateAvatarEntity(WALLET);
            world.Add(avatarEntity, new NearbyAudioStreamerComponent(SID));
            world.Add<InAudibleRangeTag>(avatarEntity);
            registry.SeedActiveStream(WALLET, SID);

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(1));
            Assert.That(world.Has<NearbyAudioSourceComponent>(avatarEntity), Is.True);
        }

        [Test]
        public void RespectsUserBlockingWhenStreamingComponentPresent()
        {
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            Entity avatarEntity = CreateAvatarEntity(WALLET);
            world.Add(avatarEntity, new NearbyAudioStreamerComponent(SID));
            world.Add<InAudibleRangeTag>(avatarEntity);
            registry.SeedActiveStream(WALLET, SID);
            userBlockingCache.UserIsBlocked(WALLET).Returns(true);

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(0),
                "blocked identity must not allocate even when archetype filter passes");
        }

        [Test]
        public void DoesNotBindAvatarWithoutAudibleRangeTagEvenWithStreamingComponent()
        {
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            Entity avatarEntity = CreateAvatarEntity(WALLET);
            world.Add(avatarEntity, new NearbyAudioStreamerComponent(SID));
            // intentionally no InAudibleRangeTag — out of range
            registry.SeedActiveStream(WALLET, SID);

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(0),
                "absent InAudibleRangeTag must skip the avatar at archetype level");
        }

        [Test]
        public void SpawnsAudioSourcePlayingAndMutedInitially()
        {
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            CreateStreamingAvatar(WALLET, SID);
            registry.SeedActiveStream(WALLET, SID);

            system.Update(0);

            NearbyAudioSourceComponent comp = GetSingleAudioComponent();
            Assert.That(comp.LivekitAudioSource.AudioSource.mute, Is.True,
                "source must start muted — burst protection on the one-frame window before PositionSystem's first tick");
            Assert.That(comp.LivekitAudioSource.AudioSource.isPlaying, Is.True,
                "factory hands sources out playing; PositionSystem owns subsequent Stop/Play transitions");
            Assert.That(comp.LivekitAudioSource.gameObject.activeSelf, Is.True);
        }

        // ── B2.1: zero-alloc data path on the per-avatar hot path ───

        [Test]
        public void BindingReadsCurrentSidFromComponentWithoutQueryingRegistryResolver()
        {
            // Verifies the dedup design goal: the per-avatar hot path reads CurrentSid from the entity,
            // never re-asks the registry for the active sid or wallet presence. Only GetActiveStream
            // (the resolve step) is allowed on the hot path. A mock registry counts data-path reads.
            INearbyAudioStreamRegistry mock = Substitute.For<INearbyAudioStreamRegistry>();
            mock.HasAudioStream(Arg.Any<string>()).ReturnsForAnyArgs(false);
            mock.GetActiveStream(Arg.Any<StreamKey>()).ReturnsForAnyArgs(Weak<AudioStream>.Null);
            mock.GetActiveSid(Arg.Any<string>()).ReturnsForAnyArgs((string?)null);
            mock.IsActiveSid(Arg.Any<StreamKey>()).ReturnsForAnyArgs(false);
            mock.IsActiveSpeaker(Arg.Any<string>()).ReturnsForAnyArgs(false);

            // Replace registry with the mock for the lifetime of this test.
            using var localStateModel = new NearbyVoiceChatStateModel(NearbyVoiceChatState.IDLE);
            var localFactory = new FakeNearbyAudioSourceFactory();
            try
            {
                var localSystem = new NearbyAudioBindingSystem(world, mock, userBlockingCache, localStateModel, localFactory, RoomMetadataCurrentScene.CreateForTest());

                const string WALLET = "wallet-alice";
                const string SID = "sid-1";
                CreateStreamingAvatar(WALLET, SID);

                // NSubstitute records every Returns(...) setup as a received call — clear before measuring.
                mock.ClearReceivedCalls();

                localSystem.Update(0);

                mock.DidNotReceive().GetActiveSid(Arg.Any<string>());
                mock.DidNotReceive().HasAudioStream(Arg.Any<string>());
                mock.DidNotReceive().IsActiveSid(Arg.Any<StreamKey>());
            }
            finally
            {
                localFactory.DisposeRoot();
            }
        }

        // ── Helpers ─────────────────────────────────────────────────

        private int CountAudioEntities() =>
            world.CountEntities(in AUDIO_SOURCE_QUERY);

        private NearbyAudioSourceComponent GetSingleAudioComponent()
        {
            NearbyAudioSourceComponent? captured = null;
            world.Query(in AUDIO_SOURCE_QUERY, (ref NearbyAudioSourceComponent c) => captured = c);
            Assert.That(captured.HasValue, Is.True, "expected exactly one NearbyAudioSourceComponent");
            return captured!.Value;
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

        // After B2.1 the binding query is gated by StreamingAudioComponent + InAudibleRangeTag;
        // the helper seeds both directly so existing trigger tests do not depend on Bridge.
        // After the resolver-dedup collapse, the component carries a single CurrentSid; tests calling
        // this helper with multi-sid signatures are obsolete (the multi-sid case can no longer exist).
        private Entity CreateStreamingAvatar(string walletId, string sid)
        {
            Entity entity = CreateAvatarEntity(walletId);
            world.Add(entity, new NearbyAudioStreamerComponent(sid));
            world.Add<InAudibleRangeTag>(entity);
            return entity;
        }

        private GameObject CreateTrackedGameObject(string name)
        {
            var go = new GameObject(name);
            gameObjects.Add(go);
            return go;
        }

        // ── Fake stream registry ────────────────────────────────────

        private sealed class FakeStreamRegistry : INearbyAudioStreamRegistry
        {
            // GetActiveStream resolution map. The data-path (sids) lives on the entity now,
            // so this fake only seeds the resolve step + the unsubscribed-race window.
            private readonly Dictionary<StreamKey, Owned<AudioStream>> streamsByKey = new ();
            private readonly HashSet<StreamKey> unsubscribed = new ();

            public void SeedActiveStream(string walletId, string sid)
            {
                var key = new StreamKey(walletId, sid);
                if (!streamsByKey.ContainsKey(key))
                    streamsByKey[key] = new Owned<AudioStream>(null!);
            }

            /// <summary>
            /// Simulates the race window where the entity still carries the sid in its
            /// <see cref="NearbyAudioStreamerComponent"/> snapshot but the underlying track was
            /// unsubscribed before <see cref="GetActiveStream"/> was called.
            /// </summary>
            public void MarkStreamAsUnsubscribed(string walletId, string sid)
            {
                var key = new StreamKey(walletId, sid);
                unsubscribed.Add(key);
                if (!streamsByKey.ContainsKey(key))
                    streamsByKey[key] = new Owned<AudioStream>(null!);
            }

            public bool HasAudioStream(string walletId) => false;

            public Weak<AudioStream> GetActiveStream(StreamKey key)
            {
                if (unsubscribed.Contains(key)) return Weak<AudioStream>.Null;

                return streamsByKey.TryGetValue(key, out Owned<AudioStream>? owned)
                    ? owned.Downgrade()
                    : Weak<AudioStream>.Null;
            }

            public string? GetActiveSid(string walletId) => null;

            public bool IsActiveSid(StreamKey key) => false;

            public bool IsActiveSpeaker(string walletId) => false;

            public int RebuildEpoch => 0;

            public void Dispose() { }
        }

        // ── Fake audio source factory ───────────────────────────────

        // Production NearbyAudioSourceFactory unconditionally invokes Construct(stream) + Play() on every
        // Create. Construct copies the supplied Weak<AudioStream> into the source, and Play() turns Unity's
        // audio thread loose on OnAudioFilterRead. With FakeStreamRegistry's contract-breached null payload,
        // OnAudioFilterRead would dereference a null AudioStream and NRE asynchronously — a race the test
        // tear-down cannot win. Tests assert bookkeeping (entity count, component fields, AudioSource flags),
        // not DSP behaviour, so this fake builds a real LivekitAudioSource MonoBehaviour, calls Play() (so
        // isPlaying assertions still hold), sets mute=true, and DELIBERATELY skips Construct(...). The
        // source's stream field stays at its default Weak<AudioStream>.Null (Disposed=true), and
        // OnAudioFilterRead short-circuits via Resource.Has=false on every audio-thread tick.
        private sealed class FakeNearbyAudioSourceFactory : INearbyAudioSourceFactory
        {
            private readonly List<LivekitAudioSource> instances = new (16);

            public LivekitAudioSource Create(StreamKey key, Weak<AudioStream> stream)
            {
                LivekitAudioSource lkSource = LivekitAudioSource.New(explicitName: true, isSpatial: true);
                lkSource.AudioSource.mute = true;
                lkSource.Play();
                instances.Add(lkSource);
                return lkSource;
            }

            public void Dispose(LivekitAudioSource? source)
            {
                if (source == null) return;
                if (!instances.Remove(source)) return;

                source.Stop();
                Object.DestroyImmediate(source.gameObject);
            }

            public void DisposeRoot()
            {
                foreach (LivekitAudioSource src in instances)
                {
                    if (src == null) continue;

                    src.Stop();
                    Object.DestroyImmediate(src.gameObject);
                }

                instances.Clear();
            }

            public void InvalidateForDeviceChange() { }
        }
    }
}
