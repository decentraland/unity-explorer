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
    /// - One audio-source entity per <c>(walletId, sid)</c> pair, created only when the avatar entity is fully ready
    ///   (Profile + AvatarBase + StreamingAudioComponent + InAudibleRangeTag, no DeleteEntityIntention).
    /// - Throttled to <see cref="NearbyAudioBindingSystem.MAX_CREATIONS_PER_FRAME"/> per tick — large crowd ramp-ups
    ///   spread across multiple frames instead of spiking a single one.
    /// - Idempotent: re-ticking with no registry changes does not duplicate bindings.
    /// - Hot path reads sids from the per-entity <see cref="NearbyAudioStreamerComponent"/>, not the registry.
    /// </summary>
    public class NearbyAudioBindingSystemShould : UnitySystemTestBase<NearbyAudioBindingSystem>
    {
        private static readonly QueryDescription AUDIO_SOURCE_QUERY = new QueryDescription().WithAll<NearbyAudioSourceComponent>();

        private static readonly FieldInfo HEAD_ANCHOR_FIELD =
            typeof(AvatarBase).GetField("<HeadAnchorPoint>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private FakeStreamRegistry registry;
        private HashSet<StreamKey> bindings;
        private IUserBlockingCache userBlockingCache;
        private NearbyVoiceChatStateModel stateModel;

        private FakeNearbyAudioSourceFactory sourceFactory;

        private readonly List<GameObject> gameObjects = new (32);

        [SetUp]
        public void SetUp()
        {
            EcsTestsUtils.SetUpFeaturesRegistry();
            RoomMetadataCurrentScene.InitializeTest();

            registry = new FakeStreamRegistry();
            bindings = new HashSet<StreamKey>();
            userBlockingCache = Substitute.For<IUserBlockingCache>();
            stateModel = new NearbyVoiceChatStateModel(NearbyVoiceChatState.IDLE);
            sourceFactory = new FakeNearbyAudioSourceFactory();

            system = new NearbyAudioBindingSystem(world, registry, bindings, userBlockingCache, stateModel, sourceFactory);
        }

        protected override void OnTearDown()
        {
            sourceFactory.DisposeRoot();

            foreach (GameObject go in gameObjects)
                if (go != null) Object.DestroyImmediate(go);

            gameObjects.Clear();

            bindings.Clear();
            stateModel.Dispose();

            RoomMetadataCurrentScene.Instance.SetBannedForTests();

            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        [Test]
        public void SingleAvatarSingleStreamCreatesOneEntity()
        {
            const string WALLET = "wallet-alice";
            Entity avatarEntity = CreateStreamingAvatar(WALLET, "sid-1");
            registry.SeedActiveStream(WALLET, "sid-1");

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(1));

            NearbyAudioSourceComponent comp = GetSingleAudioComponent();
            Assert.That(comp.Key, Is.EqualTo(new StreamKey(WALLET, "sid-1")));
            Assert.That(comp.AvatarEntity, Is.EqualTo(avatarEntity));
            Assert.That(comp.LivekitAudioSource, Is.Not.Null);
        }

        [Test]
        public void MultiStreamPerAvatarCreatesDistinctEntities()
        {
            const string WALLET = "wallet-alice";
            CreateStreamingAvatar(WALLET, "sid-1", "sid-2");
            registry.SeedActiveStream(WALLET, "sid-1");
            registry.SeedActiveStream(WALLET, "sid-2");

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(2));
        }

        [Test]
        public void AvatarWithoutAvatarBaseIsSkipped()
        {
            const string WALLET = "wallet-alice";
            Entity avatarEntity = world.Create(new Profile(WALLET, WALLET, new Avatar()));
            world.Add(avatarEntity, new NearbyAudioStreamerComponent(new[] { "sid-1" }));
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
            Assert.That(bindings.Contains(new StreamKey(WALLET, SID)), Is.False,
                "skipped creation must not poison the bindings index");
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
        public void SceneBannedIdentitySkipsCreation()
        {
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            CreateStreamingAvatar(WALLET, SID);
            registry.SeedActiveStream(WALLET, SID);
            RoomMetadataCurrentScene.Instance.SetBannedForTests(WALLET);

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(0),
                "scene-banned identity must not allocate an audio entity");
            Assert.That(bindings.Contains(new StreamKey(WALLET, SID)), Is.False,
                "skipped creation must not poison the bindings index");
        }

        [Test]
        public void SceneUnbanReBindsOnNextTick()
        {
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            CreateStreamingAvatar(WALLET, SID);
            registry.SeedActiveStream(WALLET, SID);
            RoomMetadataCurrentScene.Instance.SetBannedForTests(WALLET);

            system.Update(0);
            Assert.That(CountAudioEntities(), Is.EqualTo(0), "scene-banned tick must not allocate");

            RoomMetadataCurrentScene.Instance.SetBannedForTests();

            system.Update(0);
            Assert.That(CountAudioEntities(), Is.EqualTo(1), "lift scene ban must re-bind on the next tick");
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
            Assert.That(bindings.Contains(new StreamKey(WALLET, SID)), Is.False,
                "skipped creation must not poison the bindings index");
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
            Assert.That(bindings.Contains(new StreamKey(WALLET, SID)), Is.False);
        }

        [Test]
        public void BindsAvatarWithStreamingComponentAndAudibleRangeTag()
        {
            // Happy path mirror — pinned under the new gate name.
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            Entity avatarEntity = CreateAvatarEntity(WALLET);
            world.Add(avatarEntity, new NearbyAudioStreamerComponent(new[] { SID }));
            world.Add<InAudibleRangeTag>(avatarEntity);
            registry.SeedActiveStream(WALLET, SID);

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(1));
            Assert.That(bindings.Contains(new StreamKey(WALLET, SID)), Is.True);
        }

        [Test]
        public void RespectsUserBlockingWhenStreamingComponentPresent()
        {
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            Entity avatarEntity = CreateAvatarEntity(WALLET);
            world.Add(avatarEntity, new NearbyAudioStreamerComponent(new[] { SID }));
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
            world.Add(avatarEntity, new NearbyAudioStreamerComponent(new[] { SID }));
            // intentionally no InAudibleRangeTag — out of range
            registry.SeedActiveStream(WALLET, SID);

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(0),
                "absent InAudibleRangeTag must skip the avatar at archetype level");
            Assert.That(bindings.Contains(new StreamKey(WALLET, SID)), Is.False);
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
        public void BindingIteratesSidsFromComponentWithoutCallingRegistryGetAudioSids()
        {
            // Verifies the §1 design goal: CollectPendingCreations reads sids from the entity,
            // not the registry. A mock registry counts data-path reads — must be zero after Update.
            INearbyAudioStreamRegistry mock = Substitute.For<INearbyAudioStreamRegistry>();
            mock.GetAudioSidsArray(Arg.Any<string>()).Returns((string[]?)null);
            mock.HasAudioStream(Arg.Any<string>()).Returns(false);
            mock.GetActiveStream(Arg.Any<StreamKey>()).Returns(Weak<AudioStream>.Null);
            mock.IsStreamGone(Arg.Any<StreamKey>()).Returns(false);
            mock.IsActiveSpeaker(Arg.Any<string>()).Returns(false);

            // Replace registry with the mock for the lifetime of this test.
            var localBindings = new HashSet<StreamKey>();
            using var localStateModel = new NearbyVoiceChatStateModel(NearbyVoiceChatState.IDLE);
            var localFactory = new FakeNearbyAudioSourceFactory();
            try
            {
                var localSystem = new NearbyAudioBindingSystem(world, mock, localBindings, userBlockingCache, localStateModel, localFactory);

                const string WALLET = "wallet-alice";
                const string SID = "sid-1";
                CreateStreamingAvatar(WALLET, SID);

                // NSubstitute records every Returns(...) setup as a received call — clear before measuring.
                mock.ClearReceivedCalls();

                localSystem.Update(0);

                mock.DidNotReceive().GetAudioSidsArray(Arg.Any<string>());
                // ReadOnlySpan<string>-returning overload — also untouched by the hot path.
                // Substitute treats it like any other call; verifying the array-returning overload
                // is sufficient since both feed off the same internal storage.
                mock.DidNotReceive().HasAudioStream(Arg.Any<string>());
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
        private Entity CreateStreamingAvatar(string walletId, params string[] sids)
        {
            Entity entity = CreateAvatarEntity(walletId);
            world.Add(entity, new NearbyAudioStreamerComponent(sids));
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

            public string[]? GetAudioSidsArray(string walletId) => null;

            public Weak<AudioStream> GetActiveStream(StreamKey key)
            {
                if (unsubscribed.Contains(key)) return Weak<AudioStream>.Null;

                return streamsByKey.TryGetValue(key, out Owned<AudioStream>? owned)
                    ? owned.Downgrade()
                    : Weak<AudioStream>.Null;
            }

            public bool IsStreamGone(StreamKey key) => !streamsByKey.ContainsKey(key);

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
