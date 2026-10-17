using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.SDKComponents.InputModifier.Components;
using DCL.SyntheticInput.Components;
using DCL.SyntheticInput.Core;
using DCL.SyntheticInput.Systems;
using DCL.Time.Components;
using ECS.TestSuite;
using NUnit.Framework;
using UnityEngine;

namespace DCL.SyntheticInput.Tests
{
    public class SyntheticMovementInputSystemShould : UnitySystemTestBase<SyntheticMovementInputSystem>
    {
        private const int PHYSICS_TICK = 42;

        private Entity playerEntity;

        [SetUp]
        public void SetUp()
        {
            world.Create(new PhysicsTickComponent { Tick = PHYSICS_TICK });

            playerEntity = world.Create(
                new MovementInputComponent(),
                new JumpInputComponent(),
                new InputModifierComponent());

            system = new SyntheticMovementInputSystem(world, playerEntity);
            system.Initialize();
        }

        private UniTaskCompletionSource<SyntheticInputDelivery> AddIntent(Vector2 axes, MovementKind kind, float secondsFromNow,
            bool jump = false, bool ignoreInputModifiers = false)
        {
            var completion = new UniTaskCompletionSource<SyntheticInputDelivery>();

            world.Add(playerEntity, new SyntheticMovementIntent
            {
                Axes = axes,
                Kind = kind,
                EndTime = UnityEngine.Time.time + secondsFromNow,
                JumpRequested = jump,
                IgnoreInputModifiers = ignoreInputModifiers,
                Completion = completion,
            });

            return completion;
        }

        private ref MovementInputComponent movement => ref world.Get<MovementInputComponent>(playerEntity);

        private ref InputModifierComponent inputModifier => ref world.Get<InputModifierComponent>(playerEntity);

        [Test]
        public void ReassertAxesOverRealInputWhileHeld()
        {
            UniTaskCompletionSource<SyntheticInputDelivery> completion = AddIntent(Vector2.up, MovementKind.Jog, secondsFromNow: 100f);

            // The real input systems wrote their own values earlier this frame.
            movement.Axes = new Vector2(0.3f, -0.7f);
            movement.Kind = MovementKind.Idle;

            system.Update(0);

            Assert.That(movement.Axes, Is.EqualTo(Vector2.up));
            Assert.That(movement.Kind, Is.EqualTo(MovementKind.Jog));
            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Pending));
        }

        [Test]
        public void RestoreIdleAndCompleteOnExpiry()
        {
            UniTaskCompletionSource<SyntheticInputDelivery> completion = AddIntent(Vector2.up, MovementKind.Run, secondsFromNow: -1f);

            movement.Axes = Vector2.up;
            movement.Kind = MovementKind.Run;

            system.Update(0);

            Assert.That(movement.Axes, Is.EqualTo(Vector2.zero));
            Assert.That(movement.Kind, Is.EqualTo(MovementKind.Idle));
            Assert.That(world.Has<SyntheticMovementIntent>(playerEntity), Is.False);
            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(completion.Task.GetAwaiter().GetResult(), Is.EqualTo(SyntheticInputDelivery.Completed));
        }

        [Test]
        public void TriggerRequestedJumpOnceOnNextPhysicsTick()
        {
            AddIntent(Vector2.up, MovementKind.Jog, secondsFromNow: 100f, jump: true);

            system.Update(0);

            Assert.That(world.Get<JumpInputComponent>(playerEntity).Trigger.TickWhenJumpOccurred, Is.EqualTo(PHYSICS_TICK + 1));

            // The jump request is consumed on the first frame of the hold.
            world.Get<JumpInputComponent>(playerEntity).Trigger.TickWhenJumpOccurred = 0;
            system.Update(0);

            Assert.That(world.Get<JumpInputComponent>(playerEntity).Trigger.TickWhenJumpOccurred, Is.EqualTo(0));
        }

        [Test]
        public void CompletePreemptedHoldWhenNewerOneArrives()
        {
            UniTask<SyntheticInputDelivery> first = EcsRequest.SendAsync(world, playerEntity,
                new SyntheticMovementIntent { Axes = Vector2.up, Kind = MovementKind.Jog, EndTime = UnityEngine.Time.time + 100f },
                SyntheticInputDelivery.Preempted);

            UniTask<SyntheticInputDelivery> second = EcsRequest.SendAsync(world, playerEntity,
                new SyntheticMovementIntent { Axes = Vector2.down, Kind = MovementKind.Walk, EndTime = UnityEngine.Time.time + 100f },
                SyntheticInputDelivery.Preempted);

            Assert.That(first.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(first.GetAwaiter().GetResult(), Is.EqualTo(SyntheticInputDelivery.Preempted));
            Assert.That(second.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(world.Get<SyntheticMovementIntent>(playerEntity).Axes, Is.EqualTo(Vector2.down));
        }

        [Test]
        public void IdleTheHoldWhileSceneLocksMovement()
        {
            UniTaskCompletionSource<SyntheticInputDelivery> completion = AddIntent(Vector2.up, MovementKind.Jog, secondsFromNow: 100f);

            inputModifier.DisableAll = true;
            movement.Axes = Vector2.zero;
            movement.Kind = MovementKind.Idle;

            system.Update(0);

            Assert.That(movement.Axes, Is.EqualTo(Vector2.zero));
            Assert.That(movement.Kind, Is.EqualTo(MovementKind.Idle));
            Assert.That(completion.Task.Status, Is.EqualTo(UniTaskStatus.Pending), "the hold keeps running; only its effect is suppressed");
        }

        [Test]
        public void DegradeRunThroughTheRealFallbackTableWhenRunIsDisabled()
        {
            AddIntent(Vector2.up, MovementKind.Run, secondsFromNow: 100f);

            inputModifier.DisableRun = true;

            system.Update(0);

            Assert.That(movement.Axes, Is.EqualTo(Vector2.up));
            Assert.That(movement.Kind, Is.EqualTo(MovementKind.Jog));
        }

        [Test]
        public void BypassSceneLocksWhenTheIntentOptsOut()
        {
            AddIntent(Vector2.up, MovementKind.Run, secondsFromNow: 100f, ignoreInputModifiers: true);

            inputModifier.DisableAll = true;

            system.Update(0);

            Assert.That(movement.Axes, Is.EqualTo(Vector2.up));
            Assert.That(movement.Kind, Is.EqualTo(MovementKind.Run));
        }

        [Test]
        public void DropRequestedJumpWhenSceneDisablesJump()
        {
            AddIntent(Vector2.up, MovementKind.Jog, secondsFromNow: 100f, jump: true);

            inputModifier.DisableJump = true;

            system.Update(0);

            Assert.That(world.Get<JumpInputComponent>(playerEntity).Trigger.TickWhenJumpOccurred, Is.EqualTo(0));
            Assert.That(world.Get<SyntheticMovementIntent>(playerEntity).JumpRequested, Is.False, "the jump request is consumed even when the lock drops it");
        }
    }
}
