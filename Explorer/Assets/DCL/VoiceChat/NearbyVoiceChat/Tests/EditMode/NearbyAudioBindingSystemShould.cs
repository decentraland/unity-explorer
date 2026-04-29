using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Friends.UserBlocking;
using DCL.Profiles;
using DCL.VoiceChat.Nearby.Audio;
using DCL.VoiceChat.Nearby.Systems;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using NSubstitute;
using NUnit.Framework;
using RichTypes;
using System.Collections.Concurrent;
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
    ///   (Profile + AvatarBase, no DeleteEntityIntention).
    /// - Throttled to <see cref="NearbyAudioBindingSystem.MAX_CREATIONS_PER_FRAME"/> per tick — large crowd ramp-ups
    ///   spread across multiple frames instead of spiking a single one.
    /// - Idempotent: re-ticking with no registry changes does not duplicate bindings.
    /// </summary>
    public class NearbyAudioBindingSystemShould : UnitySystemTestBase<NearbyAudioBindingSystem>
    {
        private static readonly QueryDescription AUDIO_SOURCE_QUERY = new QueryDescription().WithAll<NearbyAudioSourceComponent>();

        private static readonly FieldInfo HEAD_ANCHOR_FIELD =
            typeof(AvatarBase).GetField("<HeadAnchorPoint>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private FakeStreamRegistry registry;
        private Dictionary<StreamKey, Entity> bindings;
        private IUserBlockingCache userBlockingCache;
        private NearbyVoiceChatStateModel stateModel;

        private VoiceChatConfiguration configuration;
        private NearbyAudioSourceFactory sourceFactory;

        private readonly List<GameObject> gameObjects = new (32);

        [SetUp]
        public void SetUp()
        {
            EcsTestsUtils.SetUpFeaturesRegistry();

            registry = new FakeStreamRegistry();
            bindings = new Dictionary<StreamKey, Entity>();
            userBlockingCache = Substitute.For<IUserBlockingCache>();
            stateModel = new NearbyVoiceChatStateModel(NearbyVoiceChatState.IDLE);
            configuration = ScriptableObject.CreateInstance<VoiceChatConfiguration>();
            sourceFactory = new NearbyAudioSourceFactory(configuration);

            system = new NearbyAudioBindingSystem(world, registry, bindings, userBlockingCache, stateModel, sourceFactory);
        }

        protected override void OnTearDown()
        {
            // Reap LivekitAudioSource instances spawned inside the system itself (parented to its private
            // sourcesRoot, not tracked in our gameObjects list). Leaving them alive across tests is fatal:
            // Unity keeps invoking OnAudioFilterRead on the audio thread, and by then the underlying world,
            // stream, and registry have been torn down — producing NREs on a foreign thread.
            foreach (LivekitAudioSource src in Object.FindObjectsByType<LivekitAudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (src == null) continue;

                src.Stop();
                src.Free();
                Object.DestroyImmediate(src.gameObject);
            }

            foreach (GameObject go in gameObjects)
                if (go != null) Object.DestroyImmediate(go);

            gameObjects.Clear();

            bindings.Clear();
            stateModel.Dispose();

            if (configuration != null) Object.DestroyImmediate(configuration);

            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        [Test]
        public void SingleAvatarSingleStreamCreatesOneEntity()
        {
            const string WALLET = "wallet-alice";
            Entity avatarEntity = CreateStreamingAvatar(WALLET);
            registry.Add(WALLET, "sid-1");

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
            CreateStreamingAvatar(WALLET);
            registry.Add(WALLET, "sid-1");
            registry.Add(WALLET, "sid-2");

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(2));
        }

        [Test]
        public void AvatarWithoutAvatarBaseIsSkipped()
        {
            const string WALLET = "wallet-alice";
            Entity avatarEntity = world.Create(new Profile(WALLET, WALLET, new Avatar()));
            world.Add<IsStreamingAudioTag>(avatarEntity);
            registry.Add(WALLET, "sid-1");

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(0),
                "no AvatarBase = pool exhausted; do not bind audio until the avatar materializes");
        }

        [Test]
        public void ThrottleCreates10ThenOver25Avatars()
        {
            const int AVATARS = 25;
            for (int i = 0; i < AVATARS; i++)
            {
                string wallet = $"wallet-{i}";
                CreateStreamingAvatar(wallet);
                registry.Add(wallet, "sid-1");
            }

            system.Update(0);
            Assert.That(CountAudioEntities(), Is.EqualTo(NearbyAudioBindingSystem.MAX_CREATIONS_PER_FRAME));

            system.Update(0);
            Assert.That(CountAudioEntities(), Is.EqualTo(NearbyAudioBindingSystem.MAX_CREATIONS_PER_FRAME * 2));

            system.Update(0);
            Assert.That(CountAudioEntities(), Is.EqualTo(AVATARS));
        }

        [Test]
        public void IdempotencyDoesNotDuplicateBindings()
        {
            const string WALLET = "wallet-alice";
            CreateStreamingAvatar(WALLET);
            registry.Add(WALLET, "sid-1");

            system.Update(0);
            system.Update(0);
            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(1));
        }

        [Test]
        public void DeleteEntityIntentionAvatarsAreFilteredOut()
        {
            const string WALLET = "wallet-alice";
            Entity avatarEntity = CreateStreamingAvatar(WALLET);
            world.Add<DeleteEntityIntention>(avatarEntity);
            registry.Add(WALLET, "sid-1");

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(0));
        }

        [Test]
        public void BlockedIdentitySkipsCreation()
        {
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            CreateStreamingAvatar(WALLET);
            registry.Add(WALLET, SID);
            userBlockingCache.UserIsBlocked(WALLET).Returns(true);

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(0),
                "blocked identity must not allocate an audio entity");
            Assert.That(bindings.ContainsKey(new StreamKey(WALLET, SID)), Is.False,
                "skipped creation must not poison the bindings index");
        }

        [Test]
        public void UnblockReBindsOnNextTick()
        {
            // The registry is untouched across the block/unblock flip; binding system must
            // re-create the audio entity once the block is lifted on the next tick.
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            CreateStreamingAvatar(WALLET);
            registry.Add(WALLET, SID);
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
                CreateStreamingAvatar(wallet);
                registry.Add(wallet, "sid-1");
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
                CreateStreamingAvatar(wallet);
                registry.Add(wallet, "sid-1");
            }

            stateModel.Disable();

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(0));
        }

        [Test]
        public void ResumeRebindsFromRegistry()
        {
            // Suppress before any tick — registry stays populated, no entity created.
            // On Resume the binding system must rehydrate from the same registry snapshot.
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            CreateStreamingAvatar(WALLET);
            registry.Add(WALLET, SID);
            stateModel.Suppress(SuppressionReason.CALL);

            system.Update(0);
            Assert.That(CountAudioEntities(), Is.EqualTo(0), "suppressed tick must not allocate");

            stateModel.Resume(SuppressionReason.CALL);

            system.Update(0);
            Assert.That(CountAudioEntities(), Is.EqualTo(1), "resume must re-bind from the untouched registry");
        }

        [Test]
        public void RaceOnSpawnSkipsCreation()
        {
            // The track was unsubscribed between GetAudioSids (collection pass) and GetActiveStream (resolve step).
            // The binding system must observe Weak<AudioStream>.Null and skip creation rather than spawn a ghost source.
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            CreateStreamingAvatar(WALLET);
            registry.Add(WALLET, SID);
            registry.MarkStreamAsUnsubscribed(WALLET, SID);

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(0),
                "Weak<AudioStream>.Null on resolve must not create an audio entity");
            Assert.That(bindings.ContainsKey(new StreamKey(WALLET, SID)), Is.False,
                "skipped creation must not poison the bindings index");
        }

        // ── A5.1: archetype gate via IsStreamingAudioTag ────────────

        [Test]
        public void DoesNotBindAvatarWithoutStreamingTagEvenIfRegistryHasSids()
        {
            // A5.1 archetype filter — Binding's query is gated by IsStreamingAudioTag. An avatar
            // without the marker must be skipped at the chunk-iteration level, even if the
            // registry already has sids for it (e.g. between Bridge ticks).
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            CreateAvatarEntity(WALLET); // intentionally no IsStreamingAudioTag
            registry.Add(WALLET, SID);

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(0),
                "absent IsStreamingAudioTag must skip the avatar at archetype level");
            Assert.That(bindings.ContainsKey(new StreamKey(WALLET, SID)), Is.False);
        }

        [Test]
        public void DoesNotBindWhenMarkerPresentButRegistryReturnedNull()
        {
            // Race-safety guard — between Bridge's tick and Binding's tick (same frame, ordered)
            // an FFI callback can drop the registry entry, leaving the marker on the avatar.
            // Binding must observe sids == null and skip creation rather than spawn a ghost.
            const string WALLET = "wallet-alice";

            Entity avatarEntity = CreateAvatarEntity(WALLET);
            world.Add<IsStreamingAudioTag>(avatarEntity);
            // registry intentionally NOT populated for WALLET

            Assert.DoesNotThrow(() => system.Update(0));

            Assert.That(CountAudioEntities(), Is.EqualTo(0),
                "marker without registry sids must not create a ghost audio entity");
        }

        [Test]
        public void BindsAvatarWithStreamingTagAndSids()
        {
            // Happy path — explicit sanity for the new gate; mirrors SingleAvatarSingleStreamCreatesOneEntity
            // but is kept under the A5.1 banner to make the marker dependency unambiguous.
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            Entity avatarEntity = CreateAvatarEntity(WALLET);
            world.Add<IsStreamingAudioTag>(avatarEntity);
            registry.Add(WALLET, SID);

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(1));
            Assert.That(bindings.ContainsKey(new StreamKey(WALLET, SID)), Is.True);
        }

        [Test]
        public void RespectsUserBlockingWhenStreamingTagPresent()
        {
            // UserIsBlocked stays a per-entity check inside the body — orthogonal to the marker.
            // Marker-present + registry-has-sids + blocked => no creation.
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            Entity avatarEntity = CreateAvatarEntity(WALLET);
            world.Add<IsStreamingAudioTag>(avatarEntity);
            registry.Add(WALLET, SID);
            userBlockingCache.UserIsBlocked(WALLET).Returns(true);

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(0),
                "blocked identity must not allocate even when archetype filter passes");
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

        // After A5.1 the binding query is gated by IsStreamingAudioTag. Tests that expect a
        // bind-result must seed the marker in addition to the registry entry — that is what
        // NearbyLivekitBridgeSystem would do in production. This helper keeps that pairing
        // explicit and mechanical.
        private Entity CreateStreamingAvatar(string walletId)
        {
            Entity entity = CreateAvatarEntity(walletId);
            world.Add<IsStreamingAudioTag>(entity);
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
            private readonly Dictionary<string, ConcurrentDictionary<string, byte>> sidsByIdentity = new ();
            private readonly Dictionary<StreamKey, Owned<AudioStream>> streamsByKey = new ();
            private readonly HashSet<StreamKey> unsubscribed = new ();

            public void Add(string walletId, string sid)
            {
                if (!sidsByIdentity.TryGetValue(walletId, out var sids))
                {
                    sids = new ConcurrentDictionary<string, byte>();
                    sidsByIdentity[walletId] = sids;
                }

                sids.TryAdd(sid, 0);

                var key = new StreamKey(walletId, sid);
                if (!streamsByKey.ContainsKey(key))
                    streamsByKey[key] = new Owned<AudioStream>(null!);
            }

            /// <summary>
            /// Simulates the race window where the registry still reports the sid in <see cref="GetAudioSids"/>
            /// but the underlying track was unsubscribed before <see cref="GetActiveStream"/> was called.
            /// </summary>
            public void MarkStreamAsUnsubscribed(string walletId, string sid) =>
                unsubscribed.Add(new StreamKey(walletId, sid));

            public ConcurrentDictionary<string, byte>? GetAudioSids(string walletId) =>
                sidsByIdentity.TryGetValue(walletId, out var sids) ? sids : null;

            public Weak<AudioStream> GetActiveStream(StreamKey key)
            {
                if (unsubscribed.Contains(key)) return Weak<AudioStream>.Null;

                return streamsByKey.TryGetValue(key, out Owned<AudioStream>? owned)
                    ? owned.Downgrade()
                    : Weak<AudioStream>.Null;
            }

            public bool IsStreamGone(StreamKey key)
            {
                ConcurrentDictionary<string, byte>? sids = GetAudioSids(key.identity);
                return sids == null || !sids.ContainsKey(key.sid);
            }

            public bool IsActiveSpeaker(string walletId) => false;

            public void Dispose() { }
        }
    }
}
