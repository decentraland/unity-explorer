using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterCamera;
using DCL.Character.Components;
using DCL.Profiles;
using DCL.VoiceChat.Nearby.Systems;
using ECS.TestSuite;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.VoiceChat.Nearby.Tests
{
    /// <summary>
    /// Documents the Nearby Audio Position System behavior — strict read+drive, no lifecycle responsibility:
    ///
    /// - Reads avatar position via <see cref="NearbyAudioSourceComponent.AvatarEntity"/>.
    /// - Drives <see cref="LivekitAudioSource"/> transform + spatial angles each frame.
    /// - Reprojects spatial position to the player head in third-person.
    ///
    /// Structural changes for audio entities are owned exclusively by <see cref="NearbyAudioCleanupSystem"/>.
    /// </summary>
    public class NearbyAudioPositionSystemShould : UnitySystemTestBase<NearbyAudioPositionSystem>
    {
        private const string PARTICIPANT_A = "wallet-alice";

        private static readonly FieldInfo HEAD_ANCHOR_FIELD =
            typeof(AvatarBase).GetField("<HeadAnchorPoint>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private readonly List<GameObject> gameObjects = new (8);

        private Entity cameraEntity;
        private Entity playerEntity;

        [SetUp]
        public void SetUp()
        {
            EcsTestsUtils.SetUpFeaturesRegistry();

            var cameraGo = CreateTrackedGameObject("TestCamera");
            var camera = cameraGo.AddComponent<Camera>();
            cameraEntity = world.Create(new CameraComponent(camera));

            var playerGo = CreateTrackedGameObject("TestPlayer");
            playerEntity = world.Create(new PlayerComponent(playerGo.transform));

            system = new NearbyAudioPositionSystem(world);
            system.Initialize();
        }

        protected override void OnTearDown()
        {
            foreach (GameObject go in gameObjects)
                if (go != null) Object.DestroyImmediate(go);

            gameObjects.Clear();

            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        // ── Position Sync ───────────────────────────────────────────

        [Test]
        public void SyncAudioSourcePositionToAvatarHeadInFirstPerson()
        {
            ref CameraComponent cam = ref world.Get<CameraComponent>(cameraEntity);
            cam.Mode = CameraMode.FirstPerson;

            Vector3 avatarPos = new Vector3(10, 0, 5);
            Vector3 headPos = avatarPos + new Vector3(0, 1.6f, 0);
            Entity avatarEntity = CreateAvatarEntity(PARTICIPANT_A, avatarPos, headPos);
            LivekitAudioSource audioSource = CreateLivekitAudioSource();
            CreateAudioEntity(PARTICIPANT_A, "sid-1", avatarEntity, audioSource);

            system.Update(0);

            Assert.That(audioSource.transform.position.x, Is.EqualTo(headPos.x).Within(0.01f));
            Assert.That(audioSource.transform.position.y, Is.EqualTo(headPos.y).Within(0.01f));
            Assert.That(audioSource.transform.position.z, Is.EqualTo(headPos.z).Within(0.01f));
        }

        [Test]
        public void ReprojectAudioSourcePositionInThirdPerson()
        {
            ref CameraComponent cam = ref world.Get<CameraComponent>(cameraEntity);
            cam.Mode = CameraMode.ThirdPerson;

            var playerGo = CreateTrackedGameObject("PlayerFocus");
            playerGo.transform.position = new Vector3(0, 1.6f, 0);
            world.Set(playerEntity, new PlayerComponent(playerGo.transform));

            Vector3 avatarPos = new Vector3(8, 0, 4);
            Vector3 remoteHead = avatarPos + new Vector3(0, 1.6f, 0);
            Entity avatarEntity = CreateAvatarEntity(PARTICIPANT_A, avatarPos, remoteHead);
            LivekitAudioSource audioSource = CreateLivekitAudioSource();
            CreateAudioEntity(PARTICIPANT_A, "sid-1", avatarEntity, audioSource);

            system.Update(0);

            Vector3 listenerPos = cam.Camera.transform.position;
            Vector3 expected = listenerPos + (remoteHead - playerGo.transform.position);

            Assert.That(audioSource.transform.position.x, Is.EqualTo(expected.x).Within(0.01f));
            Assert.That(audioSource.transform.position.y, Is.EqualTo(expected.y).Within(0.01f));
            Assert.That(audioSource.transform.position.z, Is.EqualTo(expected.z).Within(0.01f));
        }

        // ── Helpers ─────────────────────────────────────────────────

        private GameObject CreateTrackedGameObject(string name)
        {
            var go = new GameObject(name);
            gameObjects.Add(go);
            return go;
        }

        private Entity CreateAvatarEntity(string walletId, Vector3 avatarPos, Vector3 headAnchorPos)
        {
            var avatarGo = CreateTrackedGameObject($"Remote_{walletId}");
            avatarGo.transform.position = avatarPos;

            AvatarBase avatarBase = avatarGo.AddComponent<AvatarBase>();
            var headAnchorGo = CreateTrackedGameObject($"HeadAnchor_{walletId}");
            headAnchorGo.transform.SetParent(avatarGo.transform, worldPositionStays: false);
            headAnchorGo.transform.position = headAnchorPos;
            HEAD_ANCHOR_FIELD.SetValue(avatarBase, headAnchorGo.transform);

            return world.Create(
                new Profile(walletId, walletId, new Avatar()),
                avatarBase,
                new CharacterTransform(avatarGo.transform));
        }

        private Entity CreateAudioEntity(string identity, string sid, Entity avatarEntity, LivekitAudioSource source)
        {
            var key = new StreamKey(identity, sid);
            return world.Create(new NearbyAudioSourceComponent(key, avatarEntity, source));
        }

        private LivekitAudioSource CreateLivekitAudioSource()
        {
            LivekitAudioSource source = LivekitAudioSource.New();
            gameObjects.Add(source.gameObject);
            return source;
        }
    }
}
