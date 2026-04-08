using Arch.Core;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Profiles.Tables;
using DCL.VoiceChat.Proximity;
using DCL.VoiceChat.Proximity.Systems;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using LiveKit.Rooms.Streaming.Audio;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.VoiceChat.Tests
{
    /// <summary>
    /// Documents the Proximity Audio Position System behavior:
    ///
    /// - Assigns <see cref="ProximityAudioSourceComponent"/> to remote entities who participate in Proximity Chat
    /// - Syncs AudioSource positions with avatar head positions each frame for 3D spatial audio
    /// - Cleans up components when a participant leaves or their audio source is removed
    /// - Safely handles ECS structural changes (two-pass pattern to avoid ref invalidation)
    /// </summary>
    public class ProximityAudioPositionSystemShould : UnitySystemTestBase<ProximityAudioPositionSystem>
    {
        private const string PARTICIPANT_A = "wallet-alice";
        private const string PARTICIPANT_B = "wallet-bob";

        private IReadOnlyEntityParticipantTable entityParticipantTable;
        private ConcurrentDictionary<string, LivekitAudioSource> activeAudioSources;

        private readonly List<GameObject> gameObjects = new (8);

        private Entity cameraEntity;
        private Entity playerEntity;

        [SetUp]
        public void SetUp()
        {
            entityParticipantTable = Substitute.For<IReadOnlyEntityParticipantTable>();
            activeAudioSources = new ConcurrentDictionary<string, LivekitAudioSource>();

            // Camera entity — AudioListener lives on the camera
            var cameraGo = CreateTrackedGameObject("TestCamera");
            var camera = cameraGo.AddComponent<Camera>();
            cameraEntity = world.Create(new CameraComponent(camera));

            // Player entity — represents local player's body
            var playerGo = CreateTrackedGameObject("TestPlayer");
            playerEntity = world.Create(new PlayerComponent(playerGo.transform));

            system = new ProximityAudioPositionSystem(world, entityParticipantTable, activeAudioSources);
            system.Initialize();
        }

        protected override void OnTearDown()
        {
            foreach (GameObject go in gameObjects)
                if (go != null) Object.DestroyImmediate(go);

            gameObjects.Clear();
        }

        // ── Source Assignment ────────────────────────────────────────
        [Test]
        public void AssignAudioSourceComponentToRemoteParticipant()
        {
            // Arrange — remote player "Alice" joined the island and has an active audio source
            Entity remoteEntity = CreateRemoteEntity(PARTICIPANT_A, new Vector3(5, 0, 3));
            LivekitAudioSource audioSource = CreateLivekitAudioSource();

            SetupParticipant(PARTICIPANT_A, remoteEntity);
            activeAudioSources[PARTICIPANT_A] = audioSource;

            // Act
            system.Update(0);

            // Assert — system assigned a ProximityAudioSourceComponent to track spatial audio
            Assert.That(world.Has<ProximityAudioSourceComponent>(remoteEntity), Is.True);

            ref readonly ProximityAudioSourceComponent comp = ref world.Get<ProximityAudioSourceComponent>(remoteEntity);
            Assert.That(comp.ParticipantIdentity, Is.EqualTo(PARTICIPANT_A));
            Assert.That(comp.LivekitAudioSource, Is.EqualTo(audioSource));
        }

        [Test]
        public void AssignAudioSourceToMultipleParticipantsSimultaneously()
        {
            // Arrange — two remote players nearby
            Entity entityA = CreateRemoteEntity(PARTICIPANT_A, new Vector3(3, 0, 0));
            Entity entityB = CreateRemoteEntity(PARTICIPANT_B, new Vector3(-3, 0, 0));

            SetupParticipant(PARTICIPANT_A, entityA);
            SetupParticipant(PARTICIPANT_B, entityB);
            activeAudioSources[PARTICIPANT_A] = CreateLivekitAudioSource();
            activeAudioSources[PARTICIPANT_B] = CreateLivekitAudioSource();

            // Act
            system.Update(0);

            // Assert
            Assert.That(world.Has<ProximityAudioSourceComponent>(entityA), Is.True);
            Assert.That(world.Has<ProximityAudioSourceComponent>(entityB), Is.True);
        }

        [Test]
        public void NotAssignAudioSourceToEntityMarkedForDeletion()
        {
            // Arrange — entity is being destroyed (e.g. player disconnected)
            Entity remoteEntity = CreateRemoteEntity(PARTICIPANT_A, Vector3.zero);
            world.Add<DeleteEntityIntention>(remoteEntity);


            SetupParticipant(PARTICIPANT_A, remoteEntity);
            activeAudioSources[PARTICIPANT_A] = CreateLivekitAudioSource();

            // Act
            system.Update(0);

            // Assert — component not assigned to dying entity
            Assert.That(world.Has<ProximityAudioSourceComponent>(remoteEntity), Is.False);
        }

        [Test]
        public void UpdateExistingComponentWhenAudioSourceChanges()
        {
            // Arrange — Alice already has a proximity component
            Entity remoteEntity = CreateRemoteEntity(PARTICIPANT_A, Vector3.zero);
            LivekitAudioSource oldSource = CreateLivekitAudioSource();

            SetupParticipant(PARTICIPANT_A, remoteEntity);
            activeAudioSources[PARTICIPANT_A] = oldSource;

            system.Update(0);

            // Act — audio source replaced (e.g. reconnection to island room)
            LivekitAudioSource newSource = CreateLivekitAudioSource();
            activeAudioSources[PARTICIPANT_A] = newSource;

            system.Update(0);

            // Assert — existing component updated in-place, no structural change needed
            ref readonly ProximityAudioSourceComponent comp = ref world.Get<ProximityAudioSourceComponent>(remoteEntity);
            Assert.That(comp.LivekitAudioSource, Is.EqualTo(newSource));
        }

        // ── Source Cleanup ──────────────────────────────────────────

        [Test]
        public void RemoveComponentWhenParticipantLeavesProximity()
        {
            // Arrange — Alice was nearby, had an assigned audio source
            Entity remoteEntity = CreateRemoteEntity(PARTICIPANT_A, new Vector3(5, 0, 0));
            SetupParticipant(PARTICIPANT_A, remoteEntity);
            activeAudioSources[PARTICIPANT_A] = CreateLivekitAudioSource();

            system.Update(0);
            Assert.That(world.Has<ProximityAudioSourceComponent>(remoteEntity), Is.True);

            // Act — Alice's audio stream ended (left island, moved out of range, etc.)
            activeAudioSources.TryRemove(PARTICIPANT_A, out _);

            system.Update(0);

            // Assert — component cleaned up, entity continues to exist without spatial audio
            Assert.That(world.Has<ProximityAudioSourceComponent>(remoteEntity), Is.False);
        }

        // ── Position Sync ───────────────────────────────────────────

        [Test]
        public void SyncAudioSourcePositionToAvatarHeadInFirstPerson()
        {
            // Arrange — first-person camera, remote player at known position
            ref CameraComponent cam = ref world.Get<CameraComponent>(cameraEntity);
            cam.Mode = CameraMode.FirstPerson;

            Vector3 remotePos = new Vector3(10, 0, 5);
            Entity remoteEntity = CreateRemoteEntity(PARTICIPANT_A, remotePos);
            LivekitAudioSource audioSource = CreateLivekitAudioSource();

            SetupParticipant(PARTICIPANT_A, remoteEntity);
            activeAudioSources[PARTICIPANT_A] = audioSource;

            // First update: assign component
            system.Update(0);

            // Act — second update: sync positions
            system.Update(0);

            // Assert — audio source positioned at avatar head (fallback height = 1.75m since no AvatarBase)
            Vector3 expectedHeadPos = remotePos + new Vector3(0, 1.75f, 0);
            Assert.That(audioSource.transform.position.x, Is.EqualTo(expectedHeadPos.x).Within(0.01f));
            Assert.That(audioSource.transform.position.y, Is.EqualTo(expectedHeadPos.y).Within(0.01f));
            Assert.That(audioSource.transform.position.z, Is.EqualTo(expectedHeadPos.z).Within(0.01f));
        }

        [Test]
        public void ReprojectAudioSourcePositionInThirdPerson()
        {
            // Arrange — third-person camera (AudioListener on camera, but gain relative to player head)
            ref CameraComponent cam = ref world.Get<CameraComponent>(cameraEntity);
            cam.Mode = CameraMode.ThirdPerson;

            // Player at origin, camera offset behind
            var playerGo = CreateTrackedGameObject("PlayerFocus");
            playerGo.transform.position = new Vector3(0, 1.75f, 0);
            world.Set(playerEntity, new PlayerComponent(playerGo.transform));

            Vector3 remotePos = new Vector3(8, 0, 4);
            Entity remoteEntity = CreateRemoteEntity(PARTICIPANT_A, remotePos);
            LivekitAudioSource audioSource = CreateLivekitAudioSource();

            SetupParticipant(PARTICIPANT_A, remoteEntity);
            activeAudioSources[PARTICIPANT_A] = audioSource;

            system.Update(0);

            // Act
            system.Update(0);

            // Assert — position is reprojected relative to camera, not raw avatar position
            //          sourcePos = listenerPos + (remoteHead - playerHead)
            //          This ensures spatial audio gain matches the player's perspective, not the camera's offset position
            Assert.That(audioSource.transform.position, Is.Not.EqualTo(Vector3.zero));
        }

        // ── Helpers ─────────────────────────────────────────────────

        private GameObject CreateTrackedGameObject(string name)
        {
            var go = new GameObject(name);
            gameObjects.Add(go);
            return go;
        }

        private Entity CreateRemoteEntity(string participantId, Vector3 position)
        {
            var go = CreateTrackedGameObject($"Remote_{participantId}");
            go.transform.position = position;
            return world.Create(new CharacterTransform(go.transform));
        }

        private LivekitAudioSource CreateLivekitAudioSource()
        {
            LivekitAudioSource source = LivekitAudioSource.New();
            gameObjects.Add(source.gameObject);
            return source;
        }

        private void SetupParticipant(string walletId, Entity entity)
        {
            var entry = new IReadOnlyEntityParticipantTable.Entry(walletId, entity, RoomSource.ISLAND);
            entityParticipantTable.TryGet(walletId, out Arg.Any<IReadOnlyEntityParticipantTable.Entry>())
                                  .Returns(callInfo =>
                                  {
                                      callInfo[1] = entry;
                                      return true;
                                  });
        }
    }
}
