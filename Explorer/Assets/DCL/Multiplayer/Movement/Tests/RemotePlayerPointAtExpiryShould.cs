using Arch.Core;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.Multiplayer.Movement.Settings;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Pool;
using Utility.PriorityQueue;

namespace DCL.Multiplayer.Movement.Tests
{
    /// <summary>
    ///     Regression coverage for the "arm points in the wrong direction" bug: a remote point-at
    ///     assertion aims the avatar's arm at an absolute <see cref="HandPointAtComponent.WorldHitPoint" />,
    ///     and unlike the local gesture (<c>HandPointAtSystem</c>, bounded by
    ///     <see cref="ICharacterControllerSettings.PointAtDuration" />), the remote replay path had no expiry:
    ///     a single stale snapshot (e.g. a respawn/reconnect/teleport-return replay) pinned the arm at an
    ///     outdated world point forever, because nothing ever contradicted an idle pointer.
    /// </summary>
    public class RemotePlayerPointAtExpiryShould : UnitySystemTestBase<RemotePlayersMovementSystem>
    {
        private const float POINT_AT_DURATION = 1f;
        private const float MOVE_SEND_RATE = 0.1f;
        private const float STEP = 0.1f;

        private GameObject remoteGameObject;
        private Entity remoteEntity;

        [SetUp]
        public void SetUp()
        {
            var movementSettings = Substitute.For<IMultiplayerMovementSettings>();
            movementSettings.MoveSendRate.Returns(MOVE_SEND_RATE);
            movementSettings.MinTeleportDistance.Returns(100f);
            movementSettings.MinPositionDelta.Returns(0.01f);
            movementSettings.MinRotationDelta.Returns(0.01f);

            // ReSharper disable once Unity.IncorrectScriptableObjectInstantiation
            movementSettings.InterpolationSettings.Returns(new RemotePlayerInterpolationSettings
            {
                UseSpeedUp = false,
                UseBlend = false,
            });

            var characterControllerSettings = Substitute.For<ICharacterControllerSettings>();
            characterControllerSettings.RotationSpeed.Returns(10f);
            characterControllerSettings.PointAtDuration.Returns(POINT_AT_DURATION);

            system = new RemotePlayersMovementSystem(world, movementSettings, characterControllerSettings);

            remoteGameObject = new GameObject(nameof(RemotePlayerPointAtExpiryShould));
            var characterTransform = new CharacterTransform(remoteGameObject.transform);

            var queuePool = new ObjectPool<SimplePriorityQueue<NetworkMovementMessage, double>>(
                () => new SimplePriorityQueue<NetworkMovementMessage, double>(),
                actionOnRelease: queue => queue.Clear());

            remoteEntity = world.Create(
                characterTransform,
                new HeadIKComponent(),
                new HandPointAtComponent(),
                new RemotePlayerMovementComponent(queuePool),
                new InterpolationComponent(),
                new ExtrapolationComponent());

            // Pre-clear the "wait for 2 messages" interpolation-stability gate so the per-frame
            // point-at branch (where the expiry tick lives) runs starting the very next Update
            // after the first message is applied, instead of needing a throwaway priming call.
            ref RemotePlayerMovementComponent movementComponent = ref world.Get<RemotePlayerMovementComponent>(remoteEntity);
            movementComponent.InitialCooldownTime = 2 * MOVE_SEND_RATE;
            world.Set(remoteEntity, movementComponent);
        }

        protected override void OnTearDown()
        {
            world.Dispose();
            Object.DestroyImmediate(remoteGameObject);
        }

        [Test]
        public void ExpirePointingAfterPointAtDurationWithNoReassertion()
        {
            // Arrange - a single point-at snapshot arrives for a (re)spawned remote avatar, exactly
            // like the server-cached snapshot replayed via HandlePlayerJoined / HandleTeleport /
            // EmoteStarted after the reported scene-reload / teleport-return.
            var pointingMessage = new NetworkMovementMessage
            {
                timestamp = 0,
                position = Vector3.zero,
                rotationY = 0f,
                velocity = Vector3.zero,
                velocitySqrMagnitude = 0f,
                movementKind = MovementKind.Idle,
                isInstant = true,
                isPointingAt = true,
                pointAtWorldHitPoint = new Vector3(5f, 1f, 5f),
            };

            ref RemotePlayerMovementComponent movementComponent = ref world.Get<RemotePlayerMovementComponent>(remoteEntity);
            movementComponent.Enqueue(pointingMessage);
            world.Set(remoteEntity, movementComponent);

            // Act - process the first (and only) message.
            system.Update(0.1f);

            // Assert - the pointing state and IK input are applied immediately, as observed by others.
            ref RemotePlayerMovementComponent afterFirstMessage = ref world.Get<RemotePlayerMovementComponent>(remoteEntity);
            ref HandPointAtComponent handPointAtAfterFirstMessage = ref world.Get<HandPointAtComponent>(remoteEntity);
            Assert.IsTrue(afterFirstMessage.IsPointingAt, "The first message should apply the replicated pointing state");
            Assert.IsTrue(handPointAtAfterFirstMessage.IsPointing, "The first message should drive the point-at IK input");

            // Act - advance time well past PointAtDuration via repeated per-frame updates, with NO
            // further messages ever arriving -- this is the idle-pointer scenario from the bug report:
            // nothing re-asserts or contradicts the stale point-at, yet nothing should keep driving the arm.
            float elapsed = 0f;

            while (elapsed < POINT_AT_DURATION + STEP)
            {
                system.Update(STEP);
                elapsed += STEP;
            }

            // Assert - the stale point-at must have self-expired by now.
            // Unpatched: HandPointAtComponent.IsPointing/RemotePlayerMovementComponent.IsPointingAt are
            // never ticked or cleared on the remote path, so both remain stuck true indefinitely and
            // these assertions fail.
            ref HandPointAtComponent handPointAtAfterExpiry = ref world.Get<HandPointAtComponent>(remoteEntity);
            ref RemotePlayerMovementComponent movementAfterExpiry = ref world.Get<RemotePlayerMovementComponent>(remoteEntity);

            Assert.IsFalse(handPointAtAfterExpiry.IsPointing,
                "Remote pointing must stop driving the arm IK after PointAtDuration elapses with no re-assertion");
            Assert.IsFalse(movementAfterExpiry.IsPointingAt,
                "The replicated pointing flag must clear too, otherwise it re-asserts handPointAt.IsPointing every subsequent frame");
        }
    }
}
