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
    /// - Sids are read from <see cref="StreamingAudioComponent.SidsSnapshot"/> on the entity — the
    ///   data-path NEVER calls into the registry on the per-avatar hot path.
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
            Entity avatarEntity = CreateStreamingAvatar(WALLET, "sid-1");

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

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(2));
        }

        [Test]
        public void AvatarWithoutAvatarBaseIsSkipped()
        {
            const string WALLET = "wallet-alice";
            Entity avatarEntity = world.Create(new Profile(WALLET, WALLET, new Avatar()));
            world.Add(avatarEntity, new StreamingAudioComponent(new[] { "sid-1" }));
            world.Add<InAudibleRangeTag>(avatarEntity);
            registry.SeedStream(WALLET, "sid-1");

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(0),
                "no AvatarBase = pool exhausted; do not bind audio until the avatar materializes");
        }

        [Test]
        public void ThrottleCreates10ThenOver25Avatars()
        {
            const int AVATARS = 25;
            for (int i = 0; i < AVATARS; i++)
                CreateStreamingAvatar($"wallet-{i}", "sid-1");

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
            CreateStreamingAvatar(WALLET, "sid-1");

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

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(0));
        }

        [Test]
        public void BlockedIdentitySkipsCreation()
        {
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            CreateStreamingAvatar(WALLET, SID);
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
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            CreateStreamingAvatar(WALLET, SID);
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
                CreateStreamingAvatar($"wallet-{i}", "sid-1");

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
                CreateStreamingAvatar($"wallet-{i}", "sid-1");

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
            // The track was unsubscribed between Bridge's tick (which set StreamingAudioComponent)
            // and Binding's resolve step (GetActiveStream). The binding system must observe
            // Weak<AudioStream>.Null and skip creation rather than spawn a ghost source.
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            CreateStreamingAvatar(WALLET, SID);
            registry.MarkStreamAsUnsubscribed(WALLET, SID);

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(0),
                "Weak<AudioStream>.Null on resolve must not create an audio entity");
            Assert.That(bindings.ContainsKey(new StreamKey(WALLET, SID)), Is.False,
                "skipped creation must not poison the bindings index");
        }

        // ── B2.1: data-path reads from StreamingAudioComponent ──────

        [Test]
        public void DoesNotBindAvatarWithoutStreamingComponentEvenIfRegistryHasSids()
        {
            // Archetype filter — Binding's query is gated by StreamingAudioComponent. An avatar
            // without the component must be skipped at the chunk-iteration level even if the
            // registry already has sids for it (e.g. between Bridge ticks).
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            CreateAvatarEntity(WALLET); // intentionally no StreamingAudioComponent
            registry.SeedStream(WALLET, SID);

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(0),
                "absent StreamingAudioComponent must skip the avatar at archetype level");
            Assert.That(bindings.ContainsKey(new StreamKey(WALLET, SID)), Is.False);
        }

        [Test]
        public void BindsAvatarWithStreamingComponentAndAudibleRange()
        {
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            Entity avatarEntity = CreateAvatarEntity(WALLET);
            world.Add(avatarEntity, new StreamingAudioComponent(new[] { SID }));
            world.Add<InAudibleRangeTag>(avatarEntity);
            registry.SeedStream(WALLET, SID);

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(1));
            Assert.That(bindings.ContainsKey(new StreamKey(WALLET, SID)), Is.True);
        }

        [Test]
        public void RespectsUserBlockingWhenStreamingComponentPresent()
        {
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            CreateStreamingAvatar(WALLET, SID);
            userBlockingCache.UserIsBlocked(WALLET).Returns(true);

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(0),
                "blocked identity must not allocate even when archetype filter passes");
        }

        [Test]
        public void DoesNotBindAvatarWithoutAudibleRangeTagEvenWithStreamingComponent()
        {
            // Binding's query requires both StreamingAudioComponent AND InAudibleRangeTag.
            // An avatar that streams but is out of audible range must be skipped at chunk-iteration level.
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            Entity avatarEntity = CreateAvatarEntity(WALLET);
            world.Add(avatarEntity, new StreamingAudioComponent(new[] { SID }));
            // intentionally no InAudibleRangeTag — out of range
            registry.SeedStream(WALLET, SID);

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(0),
                "absent InAudibleRangeTag must skip the avatar at archetype level");
            Assert.That(bindings.ContainsKey(new StreamKey(WALLET, SID)), Is.False);
        }

        [Test]
        public void SpawnsAudioSourceDisabledInitially()
        {
            const string WALLET = "wallet-alice";
            const string SID = "sid-1";

            CreateStreamingAvatar(WALLET, SID);

            system.Update(0);

            NearbyAudioSourceComponent comp = GetSingleAudioComponent();
            Assert.That(comp.LivekitAudioSource.enabled, Is.False,
                "LivekitAudioSource must be spawned disabled — PositionSystem flips on first active tick");
            Assert.That(comp.LivekitAudioSource.AudioSource.enabled, Is.False,
                "underlying AudioSource must be spawned disabled — symmetric with the wrapper component");
        }

        [Test]
        public void BindingIteratesSidsFromComponentWithoutCallingRegistryGetAudioSids()
        {
            // Hot-path freedom from registry side-channel: data path reads sids straight from
            // StreamingAudioComponent.SidsSnapshot. registry.GetAudioSids / GetAudioSidsArray /
            // HasAudioStream MUST NOT be invoked while collecting pending creations.
            const string WALLET_A = "wallet-a";
            const string WALLET_B = "wallet-b";
            CreateStreamingAvatar(WALLET_A, "sid-1");
            CreateStreamingAvatar(WALLET_B, "sid-2", "sid-3");

            registry.ResetCallCounters();

            system.Update(0);

            Assert.That(registry.GetAudioSidsCallCount, Is.EqualTo(0),
                "data path must not query registry for sids — sids ride on the entity");
            Assert.That(registry.GetAudioSidsArrayCallCount, Is.EqualTo(0),
                "data path must not query registry for sids — sids ride on the entity");
            Assert.That(registry.HasAudioStreamCallCount, Is.EqualTo(0),
                "data path must not poll registry presence — archetype filter is the gate");

            // GetActiveStream is the resolve step — it IS expected to fire for new bindings.
            Assert.That(registry.GetActiveStreamCallCount, Is.GreaterThanOrEqualTo(1),
                "resolve step must call GetActiveStream for each pending creation");
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

        // After B2.1 the binding query is gated by StreamingAudioComponent; A1 adds InAudibleRangeTag
        // as a second mandatory clause. Tests that expect a bind-result must seed both the component
        // (with sids) and the range tag in addition to the registry stream entry — that is what
        // NearbyLivekitBridgeSystem + NearbyAudibleRangeMarkerSystem would do in production.
        private Entity CreateStreamingAvatar(string walletId, params string[] sids)
        {
            if (sids == null || sids.Length == 0) sids = new[] { "sid-1" };

            Entity entity = CreateAvatarEntity(walletId);
            world.Add(entity, new StreamingAudioComponent(sids));
            world.Add<InAudibleRangeTag>(entity);

            foreach (string sid in sids)
                registry.SeedStream(walletId, sid);

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
            private readonly Dictionary<string, string[]> sidsByIdentity = new ();
            private readonly Dictionary<StreamKey, Owned<AudioStream>> streamsByKey = new ();
            private readonly HashSet<StreamKey> unsubscribed = new ();

            public int GetAudioSidsCallCount { get; private set; }
            public int GetAudioSidsArrayCallCount { get; private set; }
            public int HasAudioStreamCallCount { get; private set; }
            public int GetActiveStreamCallCount { get; private set; }

            public void ResetCallCounters()
            {
                GetAudioSidsCallCount = 0;
                GetAudioSidsArrayCallCount = 0;
                HasAudioStreamCallCount = 0;
                GetActiveStreamCallCount = 0;
            }

            // Drives both the streams-by-key index (so resolve hits a non-null Weak) and the
            // sids-by-identity index (so legacy paths that still inspect registry state see the sid).
            public void SeedStream(string walletId, string sid)
            {
                if (!sidsByIdentity.TryGetValue(walletId, out string[]? prev))
                    sidsByIdentity[walletId] = new[] { sid };
                else if (Array.IndexOf(prev, sid) < 0)
                {
                    string[] next = new string[prev.Length + 1];
                    Array.Copy(prev, next, prev.Length);
                    next[prev.Length] = sid;
                    sidsByIdentity[walletId] = next;
                }

                var key = new StreamKey(walletId, sid);
                if (!streamsByKey.ContainsKey(key))
                    streamsByKey[key] = new Owned<AudioStream>(null!);
            }

            /// <summary>
            /// Simulates the race window where Bridge already attached the component but the
            /// underlying track was unsubscribed before <see cref="GetActiveStream"/> was called.
            /// </summary>
            public void MarkStreamAsUnsubscribed(string walletId, string sid) =>
                unsubscribed.Add(new StreamKey(walletId, sid));

            public bool HasAudioStream(string walletId)
            {
                HasAudioStreamCallCount++;
                return sidsByIdentity.ContainsKey(walletId);
            }

            public ReadOnlySpan<string> GetAudioSids(string walletId)
            {
                GetAudioSidsCallCount++;
                return sidsByIdentity.TryGetValue(walletId, out string[]? arr) ? arr : default;
            }

            public string[]? GetAudioSidsArray(string walletId)
            {
                GetAudioSidsArrayCallCount++;
                return sidsByIdentity.TryGetValue(walletId, out string[]? arr) ? arr : null;
            }

            public Weak<AudioStream> GetActiveStream(StreamKey key)
            {
                GetActiveStreamCallCount++;
                if (unsubscribed.Contains(key)) return Weak<AudioStream>.Null;

                return streamsByKey.TryGetValue(key, out Owned<AudioStream>? owned)
                    ? owned.Downgrade()
                    : Weak<AudioStream>.Null;
            }

            public bool IsStreamGone(StreamKey key)
            {
                if (!sidsByIdentity.TryGetValue(key.identity, out string[]? sids))
                    return true;
                return Array.IndexOf(sids, key.sid) < 0;
            }

            public bool IsActiveSpeaker(string walletId) => false;

            public void Dispose() { }
        }
    }
}
