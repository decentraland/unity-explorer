using Arch.Core;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.Multiplayer.Movement.Settings;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility.PriorityQueue;

namespace DCL.Multiplayer.Movement.Tests
{
    /// <summary>
    ///     Regression coverage for unity-explorer#9588 (observer side): during another player's loading/teleport
    ///     window, the sender broadcasts the spawn-position jump as ordinary (non-instant) movement. On the
    ///     observer, <see cref="RemotePlayersMovementSystem.HandleNewMessage" /> detects the first queued message
    ///     as a "teleport" (the identical-message clause in <c>CanTeleport</c> fires on a stale stand-still
    ///     duplicate), snaps to it, then dequeues the NEXT message and hands it straight to
    ///     <c>StartInterpolation</c> with no re-check — so the real spawn-position jump plays out as a visible
    ///     run/slide across the scene instead of a second snap.
    /// </summary>
    [TestFixture]
    public class RemotePlayersMovementSystemTeleportDequeueShould
    {
        private World world;
        private Entity entity;
        private RemotePlayersMovementSystem system;
        private GameObject characterGameObject;
        private ObjectPool<SimplePriorityQueue<NetworkMovementMessage, double>> queuePool;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();

            characterGameObject = new GameObject("RemotePlayer");
            characterGameObject.transform.position = Vector3.zero;
            var characterTransform = new CharacterTransform(characterGameObject.transform);

            queuePool = new ObjectPool<SimplePriorityQueue<NetworkMovementMessage, double>>(
                () => new SimplePriorityQueue<NetworkMovementMessage, double>(),
                actionOnRelease: queue => queue.Clear());

            entity = world.Create(
                characterTransform,
                new HeadIKComponent(),
                new HandPointAtComponent(),
                new RemotePlayerMovementComponent(queuePool),
                new InterpolationComponent(),
                new ExtrapolationComponent());

            IMultiplayerMovementSettings settings = Substitute.For<IMultiplayerMovementSettings>();
            settings.MoveSendRate.Returns(0.1f);
            settings.MinTeleportDistance.Returns(100f); // sqr units -> effective 10 m snap threshold, matches live asset
            settings.MinPositionDelta.Returns(0.001f);
            settings.MinRotationDelta.Returns(0.01f);
            settings.AccelerationTimeThreshold.Returns(0.5f); // matches live asset; keeps AccelerateVerySlowTransition's
                                                                // early-return so MoveKindByDistance/speed substitution
                                                                // is never needed for these short synthetic durations
            settings.UseExtrapolation.Returns(false);

            // ReSharper disable once Unity.IncorrectScriptableObjectInstantiation
            var interpolationSettings = new RemotePlayerInterpolationSettings
            {
                UseSpeedUp = true,
                UseBlend = false,
                CatchUpMessagesMin = 4,
            };

            settings.InterpolationSettings.Returns(interpolationSettings);

            ICharacterControllerSettings characterControllerSettings = Substitute.For<ICharacterControllerSettings>();
            characterControllerSettings.RotationSpeed.Returns(10f);

            system = new RemotePlayersMovementSystem(world, settings, characterControllerSettings);
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
            Object.DestroyImmediate(characterGameObject);
        }

        // Drives the entity through HandleFirstMessage + the initial-cooldown gate so subsequent messages
        // reach HandleNewMessage (the code under test), mirroring the report's repro strategy.
        private void SnapFirstMessageAndClearCooldown(Vector3 position, float rotationY, double timestamp)
        {
            ref RemotePlayerMovementComponent movementComponent = ref world.Get<RemotePlayerMovementComponent>(entity);

            movementComponent.Enqueue(new NetworkMovementMessage
            {
                timestamp = timestamp,
                position = position,
                rotationY = rotationY,
                movementKind = MovementKind.Idle,
                isInstant = false,
            });

            system.Update(0.016f); // consumes the first message via HandleFirstMessage, queue now empty

            // Push InitialCooldownTime past 2 * MoveSendRate (0.2s) with an empty queue so the next enqueue
            // reaches HandleNewMessage instead of being absorbed by the cooldown gate.
            system.Update(0.25f);
        }

        [Test]
        public void SnapInsteadOfInterpolateWhenSecondDequeueExceedsTeleportThreshold()
        {
            const double t0 = 1.0d;
            var p0 = new Vector3(0f, 0f, 0f);

            SnapFirstMessageAndClearCooldown(p0, rotationY: 0f, timestamp: t0);

            ref RemotePlayerMovementComponent movementComponent = ref world.Get<RemotePlayerMovementComponent>(entity);

            // Stale stand-still duplicate of P0 (arrives first) ...
            movementComponent.Enqueue(new NetworkMovementMessage
            {
                timestamp = 1.3d,
                position = p0,
                rotationY = 0f,
                movementKind = MovementKind.Idle,
                isInstant = false,
            });

            // ... immediately followed by the spawn-position jump, 40 m away, marked as ordinary movement
            // (the sender-side defect: isInstant is only set after loading completes).
            var spawnPosition = p0 + new Vector3(40f, 0f, 0f);

            movementComponent.Enqueue(new NetworkMovementMessage
            {
                timestamp = 1.4d,
                position = spawnPosition,
                rotationY = 0f,
                movementKind = MovementKind.Idle,
                isInstant = false,
            });

            // One frame: the identical-message clause fires on the stand-still duplicate (a no-op "teleport"),
            // then the dequeue that follows carries the real 40 m jump.
            system.Update(0.016f);

            ref InterpolationComponent intComp = ref world.Get<InterpolationComponent>(entity);
            ref CharacterTransform transComp = ref world.Get<CharacterTransform>(entity);

            // PIN (bug): the post-teleport dequeue is handed to StartInterpolation with no re-check, so the
            // avatar is mid-interpolation toward the spawn position instead of already there.
            // FIX (while-loop): CanTeleport is re-run on the second dequeue too (40 m > 10 m effective
            // threshold), so it also snaps in the same frame and interpolation never starts.
            Assert.IsFalse(intComp.Enabled,
                "Interpolation should never start for a >10 m post-teleport jump; the while-loop must re-check " +
                "CanTeleport on the dequeue that follows the stand-still duplicate and snap it too.");

            Assert.AreEqual(spawnPosition, transComp.Position,
                "The avatar should be snapped directly to the spawn position in the same frame, not mid-slide " +
                "toward it.");
        }

        [Test]
        public void HonorInstantFlagBelowDistanceThresholdAsGenuineTeleport()
        {
            const double t0 = 1.0d;
            var p0 = new Vector3(0f, 0f, 0f);

            SnapFirstMessageAndClearCooldown(p0, rotationY: 0f, timestamp: t0);

            ref RemotePlayerMovementComponent movementComponent = ref world.Get<RemotePlayerMovementComponent>(entity);

            // 5 m jump - below the 10 m effective CanTeleport distance threshold, and not close enough to
            // trip the identical-message clause either - but explicitly marked isInstant (a real teleport
            // marker, e.g. a small in-loading spawn correction).
            var nearTeleportPosition = p0 + new Vector3(5f, 0f, 0f);

            movementComponent.Enqueue(new NetworkMovementMessage
            {
                timestamp = 1.1d,
                position = nearTeleportPosition,
                rotationY = 0f,
                movementKind = MovementKind.Idle,
                isInstant = true,
            });

            system.Update(0.016f);

            ref RemotePlayerMovementComponent afterMovement = ref world.Get<RemotePlayerMovementComponent>(entity);

            // Interpolation.Execute already snaps immediately once End.isInstant is true (Interpolation.cs:20,40),
            // so the final transform position converges to the same place either way in a single frame - the
            // observable seam for site 2 is RemotePlayerMovementComponent.WasTeleported: only the CanTeleport
            // disjunct routes the message through TeleportFiltered (which sets WasTeleported: true and runs the
            // catch-up duplicate filter); without it, the message falls through to StartInterpolation, whose
            // Interpolate() -> AddPassed() call defaults WasTeleported back to false even though the message
            // was a genuine teleport marker.
            //
            // PIN (bug): CanTeleport ignores remote.isInstant entirely -> false here (5 m < 10 m, not identical)
            //            -> WasTeleported ends up false.
            // FIX: `remote.isInstant ||` makes CanTeleport true -> routed through TeleportFiltered
            //      -> WasTeleported ends up true.
            Assert.IsTrue(afterMovement.WasTeleported,
                "A message explicitly marked isInstant should be honored as a real teleport (WasTeleported: true) " +
                "even when it falls below the distance-based snap threshold.");
        }
    }
}
