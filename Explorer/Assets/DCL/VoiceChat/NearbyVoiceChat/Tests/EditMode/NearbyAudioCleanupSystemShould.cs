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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;
using Object = UnityEngine.Object;

namespace DCL.VoiceChat.Nearby.Tests
{
    /// <summary>
    /// Documents <see cref="NearbyAudioCleanupSystem"/> contract:
    ///
    /// - Owns ALL structural changes to Nearby audio-source entities (binding system creates, cleanup destroys).
    /// - Pull-based detection: per tick, three triggers per entity — avatar gone, stream gone, identity blocked — with first-match-wins.
    /// - Teardown is atomic: <see cref="LivekitAudioSource.Stop"/> → <see cref="LivekitAudioSource.Free"/> →
    ///   <c>SafeDestroyGameObject</c> → <c>bindings.Remove</c> → <c>World.Destroy</c>.
    /// - <see cref="ECS.LifeCycle.IFinalizeWorldSystem.FinalizeComponents"/> disposes any survivors and clears bindings.
    /// </summary>
    public class NearbyAudioCleanupSystemShould : UnitySystemTestBase<NearbyAudioCleanupSystem>
    {
        private const string PARTICIPANT_A = "wallet-alice";
        private const string SID_1 = "sid-1";

        private static readonly QueryDescription AUDIO_SOURCE_QUERY = new QueryDescription().WithAll<NearbyAudioSourceComponent>();

        private static readonly FieldInfo HEAD_ANCHOR_FIELD =
            typeof(AvatarBase).GetField("<HeadAnchorPoint>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private FakeStreamRegistry registry = null!;
        private Dictionary<StreamKey, Entity> bindings = null!;
        private IUserBlockingCache userBlockingCache = null!;
        private NearbyVoiceChatStateModel stateModel = null!;
        private VoiceChatConfiguration configuration = null!;
        private NearbyAudioSourceFactory sourceFactory = null!;
        private readonly List<GameObject> gameObjects = new (16);

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

            system = new NearbyAudioCleanupSystem(world, registry, bindings, userBlockingCache, stateModel, sourceFactory);
        }

        protected override void OnTearDown()
        {
            foreach (GameObject go in gameObjects)
                if (go != null) Object.DestroyImmediate(go);

            gameObjects.Clear();
            bindings.Clear();
            stateModel.Dispose();

            if (configuration != null) Object.DestroyImmediate(configuration);

            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        // ── Trigger #1: avatar gone ─────────────────────────────────

        [Test]
        public void DeadAvatarEntityCausesCleanup()
        {
            (Entity audioEntity, Entity avatarEntity, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            world.Destroy(avatarEntity);

            system.Update(0);

            Assert.That(world.IsAlive(audioEntity), Is.False);
            Assert.That(source == null, Is.True, "LivekitAudioSource must be destroyed");
            Assert.That(bindings.ContainsKey(new StreamKey(PARTICIPANT_A, SID_1)), Is.False);
        }

        [Test]
        public void AvatarWithDeleteEntityIntentionCausesCleanup()
        {
            (Entity audioEntity, Entity avatarEntity, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            world.Add<DeleteEntityIntention>(avatarEntity);

            system.Update(0);

            Assert.That(world.IsAlive(audioEntity), Is.False);
            Assert.That(source == null, Is.True);
            Assert.That(bindings.ContainsKey(new StreamKey(PARTICIPANT_A, SID_1)), Is.False);
        }

        [Test]
        public void AvatarMissingAvatarBaseCausesCleanup()
        {
            (Entity audioEntity, Entity avatarEntity, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            world.Remove<AvatarBase>(avatarEntity);

            system.Update(0);

            Assert.That(world.IsAlive(audioEntity), Is.False);
            Assert.That(source == null, Is.True);
            Assert.That(bindings.ContainsKey(new StreamKey(PARTICIPANT_A, SID_1)), Is.False);
        }

        // ── Trigger #2: stream gone ─────────────────────────────────

        [Test]
        public void RegistryMissingWalletCausesCleanup()
        {
            (Entity audioEntity, _, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            registry.RemoveAll(PARTICIPANT_A);

            system.Update(0);

            Assert.That(world.IsAlive(audioEntity), Is.False);
            Assert.That(source == null, Is.True);
            Assert.That(bindings.ContainsKey(new StreamKey(PARTICIPANT_A, SID_1)), Is.False);
        }

        [Test]
        public void RegistryMissingSidCausesCleanup()
        {
            (Entity audioEntity, _, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            registry.Add(PARTICIPANT_A, "sid-other"); // wallet still in registry, but bound sid is gone
            registry.RemoveSid(PARTICIPANT_A, SID_1);

            system.Update(0);

            Assert.That(world.IsAlive(audioEntity), Is.False);
            Assert.That(source == null, Is.True);
            Assert.That(bindings.ContainsKey(new StreamKey(PARTICIPANT_A, SID_1)), Is.False);
        }

        // ── Trigger #3: blocked identity ────────────────────────────

        [Test]
        public void BlockedIdentityCausesCleanup()
        {
            (Entity audioEntity, _, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            userBlockingCache.UserIsBlocked(PARTICIPANT_A).Returns(true);

            system.Update(0);

            Assert.That(world.IsAlive(audioEntity), Is.False);
            Assert.That(source == null, Is.True, "LivekitAudioSource must be destroyed");
            Assert.That(bindings.ContainsKey(new StreamKey(PARTICIPANT_A, SID_1)), Is.False);
        }

        // ── Trigger #4: listening gate ──────────────────────────────

        [Test]
        public void SuppressedStateTearsDownAllAudioEntities()
        {
            const int COUNT = 3;
            var sources = new List<LivekitAudioSource>(COUNT);

            for (int i = 0; i < COUNT; i++)
            {
                (_, _, LivekitAudioSource source) = SeedBinding($"wallet-{i}", SID_1);
                sources.Add(source);
            }

            stateModel.Suppress(SuppressionReason.CALL);

            system.Update(0);

            Assert.That(world.CountEntities(in AUDIO_SOURCE_QUERY), Is.EqualTo(0));
            Assert.That(bindings, Is.Empty);
            foreach (LivekitAudioSource source in sources)
                Assert.That(source == null, Is.True, "LivekitAudioSource must be physically destroyed");
        }

        [Test]
        public void DisabledStateTearsDownAllAudioEntities()
        {
            const int COUNT = 3;
            var sources = new List<LivekitAudioSource>(COUNT);

            for (int i = 0; i < COUNT; i++)
            {
                (_, _, LivekitAudioSource source) = SeedBinding($"wallet-{i}", SID_1);
                sources.Add(source);
            }

            stateModel.Disable();

            system.Update(0);

            Assert.That(world.CountEntities(in AUDIO_SOURCE_QUERY), Is.EqualTo(0));
            Assert.That(bindings, Is.Empty);
            foreach (LivekitAudioSource source in sources)
                Assert.That(source == null, Is.True);
        }

        // ── Compound ────────────────────────────────────────────────

        [Test]
        public void BothTriggersPresentResultInSingleTeardown()
        {
            (Entity audioEntity, Entity avatarEntity, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);
            world.Destroy(avatarEntity);
            registry.RemoveAll(PARTICIPANT_A);

            Assert.DoesNotThrow(() => system.Update(0));

            Assert.That(world.IsAlive(audioEntity), Is.False);
            Assert.That(source == null, Is.True);
            Assert.That(bindings.ContainsKey(new StreamKey(PARTICIPANT_A, SID_1)), Is.False);
        }

        [Test]
        public void MassCleanupOnDisconnected()
        {
            const int COUNT = 10;
            var sources = new List<LivekitAudioSource>(COUNT);

            for (int i = 0; i < COUNT; i++)
            {
                (_, _, LivekitAudioSource source) = SeedBinding($"wallet-{i}", SID_1);
                sources.Add(source);
            }

            registry.ClearAll();

            system.Update(0);

            Assert.That(world.CountEntities(in AUDIO_SOURCE_QUERY), Is.EqualTo(0));
            foreach (LivekitAudioSource source in sources)
                Assert.That(source == null, Is.True);
        }

        // ── Idempotency ─────────────────────────────────────────────

        [Test]
        public void IdleTickWithNoTriggersDoesNotMutateWorld()
        {
            (Entity audioEntity, _, LivekitAudioSource source) = SeedBinding(PARTICIPANT_A, SID_1);

            system.Update(0);
            system.Update(0);

            Assert.That(world.IsAlive(audioEntity), Is.True);
            Assert.That(source == null, Is.False);
            Assert.That(bindings.ContainsKey(new StreamKey(PARTICIPANT_A, SID_1)), Is.True);
            Assert.That(world.CountEntities(in AUDIO_SOURCE_QUERY), Is.EqualTo(1));
        }

        // ── Finalize ────────────────────────────────────────────────

        [Test]
        public void FinalizeDestroysAllRemainingSourcesAndClearsBindings()
        {
            const int COUNT = 3;
            var sources = new List<LivekitAudioSource>(COUNT);

            for (int i = 0; i < COUNT; i++)
            {
                (_, _, LivekitAudioSource source) = SeedBinding($"wallet-{i}", SID_1);
                sources.Add(source);
            }

            // Even with all triggers cold, finalize must dispose everything.
            system.FinalizeComponents(world.Query(in AUDIO_SOURCE_QUERY));

            foreach (LivekitAudioSource source in sources)
                Assert.That(source == null, Is.True);

            Assert.That(bindings.ContainsKey(new StreamKey("wallet-0", SID_1)), Is.False);
        }

        // ── Helpers ─────────────────────────────────────────────────

        private (Entity audioEntity, Entity avatarEntity, LivekitAudioSource source) SeedBinding(string walletId, string sid)
        {
            Entity avatarEntity = CreateAvatarEntity(walletId);
            registry.Add(walletId, sid);

            LivekitAudioSource source = CreateLivekitAudioSource();
            var key = new StreamKey(walletId, sid);
            Entity audioEntity = world.Create(new NearbyAudioSourceComponent(key, avatarEntity, source));
            bindings.TryAdd(key, audioEntity);

            return (audioEntity, avatarEntity, source);
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

        private LivekitAudioSource CreateLivekitAudioSource()
        {
            LivekitAudioSource source = LivekitAudioSource.New();
            gameObjects.Add(source.gameObject);
            return source;
        }

        private GameObject CreateTrackedGameObject(string name)
        {
            var go = new GameObject(name);
            gameObjects.Add(go);
            return go;
        }

        // ── Fake stream registry (mutable, for cleanup-trigger driving) ─

        private sealed class FakeStreamRegistry : INearbyAudioStreamRegistry
        {
            private readonly Dictionary<string, ConcurrentDictionary<string, byte>> sidsByIdentity = new ();

            public void Add(string walletId, string sid)
            {
                if (!sidsByIdentity.TryGetValue(walletId, out var sids))
                {
                    sids = new ConcurrentDictionary<string, byte>();
                    sidsByIdentity[walletId] = sids;
                }

                sids.TryAdd(sid, 0);
            }

            public void RemoveAll(string walletId) =>
                sidsByIdentity.Remove(walletId);

            public void RemoveSid(string walletId, string sid)
            {
                if (sidsByIdentity.TryGetValue(walletId, out var sids))
                    sids.TryRemove(sid, out _);
            }

            public void ClearAll() =>
                sidsByIdentity.Clear();

            public ConcurrentDictionary<string, byte>? GetAudioSids(string walletId) =>
                sidsByIdentity.TryGetValue(walletId, out var sids) ? sids : null;

            public Weak<AudioStream> GetActiveStream(StreamKey key) =>
                Weak<AudioStream>.Null;

            public bool IsStreamGone(StreamKey key) =>
                throw new NotImplementedException();

            public void Dispose() { }
        }
    }
}
