using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterMotion.Components;
using DCL.ECSComponents;
using DCL.SyntheticInput.Components;
using DCL.SyntheticInput.Core;
using NUnit.Framework;
using UnityEngine;

namespace DCL.SyntheticInput.Tests
{
    public class SyntheticInputAgentShould
    {
        private World world = null!;
        private Entity playerEntity;
        private SyntheticInputAgent agent = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            playerEntity = world.Create();
            agent = new SyntheticInputAgent(world, playerEntity);
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        private SyntheticPointerEventIntent currentPointerIntent => world.Get<SyntheticPointerEventIntent>(playerEntity);

        private void CompletePointerIntent(SyntheticPointerOutcome outcome) =>
            EcsRequest.CompleteAndRemove(world, playerEntity, currentPointerIntent, outcome);

        private static SyntheticPointerOutcome Delivered(int entityId, int crdtId = 0, SyntheticPressHandoff? press = null) =>
            new ()
            {
                Result = new SyntheticPointerResult { Hit = true, SceneEntityId = entityId, CrdtEntityId = crdtId },
                Press = press,
            };

        [Test]
        public void ComposeAClickFromOrderedPressAndReleaseLegs()
        {
            UniTask<SyntheticPointerResult> click = agent.ClickAsync(PointerAim.AtEntity(7, "scene-a"), InputAction.IaPointer, timeoutSec: 30f);

            SyntheticPointerEventIntent press = currentPointerIntent;
            Assert.That(press.EventType, Is.EqualTo(PointerEventType.PetDown));
            Assert.That(press.TargetEntityId, Is.EqualTo(7));
            Assert.That(press.SceneId, Is.EqualTo("scene-a"));
            Assert.That(press.Press, Is.Null);

            var handoff = new SyntheticPressHandoff { World = world, Entity = playerEntity, Tick = 3 };
            CompletePointerIntent(Delivered(7, press: handoff));

            SyntheticPointerEventIntent release = currentPointerIntent;
            Assert.That(release.EventType, Is.EqualTo(PointerEventType.PetUp));
            Assert.That(release.Press, Is.Not.Null);
            Assert.That(release.Press!.Value.Tick, Is.EqualTo(3u));

            CompletePointerIntent(Delivered(7, crdtId: 99));

            Assert.That(click.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            SyntheticPointerResult result = click.GetAwaiter().GetResult();
            Assert.That(result.Hit, Is.True);
            Assert.That(result.CrdtEntityId, Is.EqualTo(99));
        }

        [Test]
        public void MergePressDiagnosticsWhenTheReleaseMisses()
        {
            UniTask<SyntheticPointerResult> click = agent.ClickAsync(PointerAim.AtEntity(7), InputAction.IaPointer, timeoutSec: 30f);

            CompletePointerIntent(Delivered(7, crdtId: 40, press: new SyntheticPressHandoff { World = world, Entity = playerEntity, Tick = 3 }));

            CompletePointerIntent(new SyntheticPointerOutcome
            {
                Result = new SyntheticPointerResult { Hit = false, FailureReason = "another collider blocks", BlockedByCrdtId = 5 },
            });

            SyntheticPointerResult result = click.GetAwaiter().GetResult();
            Assert.That(result.Hit, Is.True, "the press was delivered; the merged result keeps reporting it");
            Assert.That(result.CrdtEntityId, Is.EqualTo(40));
            Assert.That(result.UpRayMissed, Is.True);
            Assert.That(result.FailureReason, Does.Contain("the scene received only the press"));
            Assert.That(result.BlockedByCrdtId, Is.EqualTo(5));
        }

        [Test]
        public void SkipTheReleaseLegWhenThePressMisses()
        {
            UniTask<SyntheticPointerResult> click = agent.ClickAsync(PointerAim.AtEntity(7), InputAction.IaPointer, timeoutSec: 30f);

            CompletePointerIntent(new SyntheticPointerOutcome
            {
                Result = new SyntheticPointerResult { Hit = false, FailureReason = "out of range" },
            });

            Assert.That(click.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(click.GetAwaiter().GetResult().FailureReason, Is.EqualTo("out of range"));
            Assert.That(world.Has<SyntheticPointerEventIntent>(playerEntity), Is.False, "no release leg may follow a missed press");
        }

        [Test]
        public void DeliverALonePressWithoutAReleaseLeg()
        {
            UniTask<SyntheticPointerResult> down = agent.PointerDownAsync(PointerAim.AtEntity(7), InputAction.IaPrimary, timeoutSec: 30f);

            CompletePointerIntent(Delivered(7, press: new SyntheticPressHandoff { World = world, Entity = playerEntity, Tick = 3 }));

            Assert.That(down.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(down.GetAwaiter().GetResult().Hit, Is.True);
            Assert.That(world.Has<SyntheticPointerEventIntent>(playerEntity), Is.False);
        }

        /// <summary>
        ///     The sweep is the held-and-turn gesture: the press arms whatever watches for a pointer-down, the
        ///     camera hold is what moves the ray a scene samples, and the release closes it. The order is the
        ///     contract — a camera turn with nothing held sweeps nothing.
        /// </summary>
        [Test]
        public void ComposeASweepFromPressCameraLookAndRelease()
        {
            UniTask<SyntheticSweepResult> sweep = agent.SweepAsync(PointerAim.AtEntity(7), InputAction.IaPointer, new Vector2(5f, 0f), seconds: 0.5f, timeoutSec: 30f);

            Assert.That(currentPointerIntent.EventType, Is.EqualTo(PointerEventType.PetDown));
            Assert.That(world.Has<SyntheticCameraLookIntent>(playerEntity), Is.False, "the camera must not turn before the press landed");

            CompletePointerIntent(Delivered(7, press: new SyntheticPressHandoff { World = world, Entity = playerEntity, Tick = 3 }));

            Assert.That(world.Has<SyntheticCameraLookIntent>(playerEntity), Is.True, "the press landed, so the camera sweep runs while it is held");
            Assert.That(world.Get<SyntheticCameraLookIntent>(playerEntity).AxisValue, Is.EqualTo(new Vector2(5f, 0f)));

            EcsRequest.CompleteAndRemove(world, playerEntity, world.Get<SyntheticCameraLookIntent>(playerEntity), SyntheticInputDelivery.Completed);

            Assert.That(currentPointerIntent.EventType, Is.EqualTo(PointerEventType.PetUp), "the button is released after the camera turned");
            CompletePointerIntent(Delivered(7));

            Assert.That(sweep.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            SyntheticSweepResult result = sweep.GetAwaiter().GetResult();
            Assert.That(result.FailureReason, Is.Null);
            Assert.That(result.Press.Hit, Is.True);
            Assert.That(result.CameraSweep, Is.EqualTo(SyntheticInputDelivery.Completed));
            Assert.That(result.Release.Hit, Is.True);
        }

        [Test]
        public void AbandonASweepWhosePressWasNeverDelivered()
        {
            UniTask<SyntheticSweepResult> sweep = agent.SweepAsync(PointerAim.AtEntity(7), InputAction.IaPointer, new Vector2(5f, 0f), seconds: 0.5f, timeoutSec: 30f);

            CompletePointerIntent(new SyntheticPointerOutcome { Result = new SyntheticPointerResult { Hit = false, FailureReason = "out of range" } });

            Assert.That(sweep.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            SyntheticSweepResult result = sweep.GetAwaiter().GetResult();
            Assert.That(result.FailureReason, Does.Contain("out of range"));
            Assert.That(world.Has<SyntheticCameraLookIntent>(playerEntity), Is.False, "turning the camera with nothing held is not the gesture that was asked for");
            Assert.That(world.Has<SyntheticPointerEventIntent>(playerEntity), Is.False, "no release may follow a press that never landed");
        }

        [Test]
        public void PreemptThePendingGestureWhenANewerOneStarts()
        {
            UniTask<SyntheticPointerResult> first = agent.ClickAsync(PointerAim.AtEntity(7), InputAction.IaPointer, timeoutSec: 30f);
            agent.ClickAsync(PointerAim.AtEntity(8), InputAction.IaPointer, timeoutSec: 30f).Forget();

            Assert.That(first.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(first.GetAwaiter().GetResult().FailureReason, Does.Contain("preempted"));
            Assert.That(world.Get<SyntheticPointerEventIntent>(playerEntity).TargetEntityId, Is.EqualTo(8));
        }

        /// <summary>
        ///     The entity-bound half of the global fan-out is only reachable with an aim: a driver has no OS cursor
        ///     resting on a target for the reticle to follow, so an aimless edge always lands on the scene root.
        /// </summary>
        [Test]
        public void AimTheGlobalGestureAtAnEntityWhenOneIsRequested()
        {
            UniTask<SyntheticPointerResult> press = agent.GlobalInputAsync(InputAction.IaPrimary, holdSeconds: 0f, PointerAim.AtEntity(77));

            SyntheticPointerEventIntent down = currentPointerIntent;
            Assert.That(down.HasAimTarget, Is.True);
            Assert.That(down.TargetEntityId, Is.EqualTo(77));
            Assert.That(down.Button, Is.EqualTo(InputAction.IaPrimary));
            Assert.That(down.EventType, Is.EqualTo(PointerEventType.PetDown));

            CompletePointerIntent(new SyntheticPointerOutcome
            {
                Result = new SyntheticPointerResult { Hit = true, SceneEntityId = 77 },
                Press = new SyntheticPressHandoff { World = world, Entity = Entity.Null, Tick = 3 },
            });

            SyntheticPointerEventIntent up = currentPointerIntent;
            Assert.That(up.EventType, Is.EqualTo(PointerEventType.PetUp));
            Assert.That(up.TargetEntityId, Is.EqualTo(77));
            Assert.That(up.Press!.Value.Tick, Is.EqualTo(3u));

            CompletePointerIntent(new SyntheticPointerOutcome
            {
                Result = new SyntheticPointerResult { Hit = true, SceneEntityId = 77 },
            });

            Assert.That(press.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(press.GetAwaiter().GetResult().Hit, Is.True);
        }

        [Test]
        public void ComposeAGlobalPressAndReleaseFromAimlessIntents()
        {
            UniTask<SyntheticPointerResult> press = agent.GlobalInputAsync(InputAction.IaPrimary);

            SyntheticPointerEventIntent down = currentPointerIntent;
            Assert.That(down.HasAimTarget, Is.False);
            Assert.That(down.Button, Is.EqualTo(InputAction.IaPrimary));
            Assert.That(down.EventType, Is.EqualTo(PointerEventType.PetDown));

            CompletePointerIntent(new SyntheticPointerOutcome
            {
                Result = new SyntheticPointerResult { Hit = false, SceneEntityId = -1 },
                Press = new SyntheticPressHandoff { World = world, Entity = Entity.Null, Tick = 12 },
            });

            SyntheticPointerEventIntent up = currentPointerIntent;
            Assert.That(up.EventType, Is.EqualTo(PointerEventType.PetUp));
            Assert.That(up.HasAimTarget, Is.False);
            Assert.That(up.Press!.Value.Tick, Is.EqualTo(12u));

            CompletePointerIntent(new SyntheticPointerOutcome
            {
                Result = new SyntheticPointerResult { Hit = false, SceneEntityId = -1 },
            });

            Assert.That(press.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(press.GetAwaiter().GetResult().FailureReason, Is.Null);
        }

        [Test]
        public void InstallTheMovementHoldWithTheRequestedParameters()
        {
            UniTask<SyntheticInputDelivery> walk = agent.WalkAsync(Vector2.up, MovementKind.Run, seconds: 2f, jump: true, ignoreInputModifiers: true);

            SyntheticMovementIntent intent = world.Get<SyntheticMovementIntent>(playerEntity);
            Assert.That(intent.Axes, Is.EqualTo(Vector2.up));
            Assert.That(intent.Kind, Is.EqualTo(MovementKind.Run));
            Assert.That(intent.JumpRequested, Is.True);
            Assert.That(intent.IgnoreInputModifiers, Is.True);
            Assert.That(intent.EndTime, Is.EqualTo(UnityEngine.Time.time + 2f).Within(0.5f));

            EcsRequest.CompleteAndRemove(world, playerEntity, intent, SyntheticInputDelivery.Completed);

            Assert.That(walk.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(walk.GetAwaiter().GetResult(), Is.EqualTo(SyntheticInputDelivery.Completed));
        }
    }
}
