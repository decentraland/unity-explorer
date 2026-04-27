using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Profiles;
using DCL.VoiceChat.Nearby.Audio;
using DCL.VoiceChat.Nearby.Systems;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using NUnit.Framework;
using RichTypes;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;

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
        private VoiceChatConfiguration configuration;

        private readonly List<GameObject> gameObjects = new (32);

        [SetUp]
        public void SetUp()
        {
            EcsTestsUtils.SetUpFeaturesRegistry();

            registry = new FakeStreamRegistry();
            configuration = ScriptableObject.CreateInstance<VoiceChatConfiguration>();

            system = new NearbyAudioBindingSystem(world, registry, configuration);
        }

        protected override void OnTearDown()
        {
            foreach (GameObject go in gameObjects)
                if (go != null) Object.DestroyImmediate(go);

            gameObjects.Clear();

            if (configuration != null) Object.DestroyImmediate(configuration);

            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        [Test]
        public void SingleAvatarSingleStreamCreatesOneEntity()
        {
            const string WALLET = "wallet-alice";
            Entity avatarEntity = CreateAvatarEntity(WALLET);
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
            CreateAvatarEntity(WALLET);
            registry.Add(WALLET, "sid-1");
            registry.Add(WALLET, "sid-2");

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(2));
        }

        [Test]
        public void AvatarWithoutAvatarBaseIsSkipped()
        {
            const string WALLET = "wallet-alice";
            world.Create(new Profile(WALLET, WALLET, new Avatar()));
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
                CreateAvatarEntity(wallet);
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
            CreateAvatarEntity(WALLET);
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
            Entity avatarEntity = CreateAvatarEntity(WALLET);
            world.Add<DeleteEntityIntention>(avatarEntity);
            registry.Add(WALLET, "sid-1");

            system.Update(0);

            Assert.That(CountAudioEntities(), Is.EqualTo(0));
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

            public void Add(string walletId, string sid)
            {
                if (!sidsByIdentity.TryGetValue(walletId, out var sids))
                {
                    sids = new ConcurrentDictionary<string, byte>();
                    sidsByIdentity[walletId] = sids;
                }

                sids.TryAdd(sid, 0);
            }

            public ConcurrentDictionary<string, byte>? GetAudioSids(string walletId) =>
                sidsByIdentity.TryGetValue(walletId, out var sids) ? sids : null;

            public Weak<AudioStream> GetActiveStream(StreamKey key) =>
                Weak<AudioStream>.Null;

            public void Dispose() { }
        }
    }
}
