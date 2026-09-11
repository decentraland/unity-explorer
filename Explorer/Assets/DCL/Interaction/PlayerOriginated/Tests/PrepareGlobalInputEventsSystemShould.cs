using Arch.Core;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.PlayerOriginated.Systems;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using InputAction = DCL.ECSComponents.InputAction;
using PointerEventType = DCL.ECSComponents.PointerEventType;

namespace DCL.Interaction.PlayerOriginated.Tests
{
    public class PrepareGlobalInputEventsSystemShould : InputTestFixture
    {
        private World world = null!;
        private GlobalInputEvents globalInputEvents = null!;
        private PlayerInteractionEntity playerInteractionEntity;
        private PrepareGlobalInputEventsSystem system = null!;
        private Keyboard keyboard = null!;
        private UnityEngine.InputSystem.InputAction primaryAction = null!;

        [SetUp]
        public void SetUp()
        {
            base.Setup();

            world = World.Create();
            keyboard = InputSystem.AddDevice<Keyboard>();

            primaryAction = new UnityEngine.InputSystem.InputAction(binding: "<Keyboard>/e");
            primaryAction.Enable();

            var actionsMap = new Dictionary<InputAction, UnityEngine.InputSystem.InputAction>
            {
                { InputAction.IaPrimary, primaryAction },
            };

            globalInputEvents = new GlobalInputEvents();

            Entity pipelineEntity = world.Create(new SyntheticPointerInput());
            playerInteractionEntity = new PlayerInteractionEntity(pipelineEntity, world, world.Create());

            system = new PrepareGlobalInputEventsSystem(world, globalInputEvents, actionsMap, playerInteractionEntity);
        }

        [TearDown]
        public void DisposeWorld()
        {
            primaryAction.Disable();
            world.Dispose();
        }

        private void PostSynthetic(InputAction? press, InputAction? release, int frameOffset = 0)
        {
            playerInteractionEntity.SyntheticPointerInput = new SyntheticPointerInput
            {
                PressButton = press,
                ReleaseButton = release,
                PostedAtFrame = UnityEngine.Time.frameCount + frameOffset,
            };
        }

        private static IGlobalInputEvents.Entry Entry(InputAction action, PointerEventType type) =>
            new (action, type);

        [Test]
        public void AppendSyntheticEdgesAfterTheRealOnes()
        {
            PostSynthetic(InputAction.IaSecondary, InputAction.IaAction3);

            system.Update(0);

            Assert.That(globalInputEvents.Entries, Is.EqualTo(new[]
            {
                Entry(InputAction.IaSecondary, PointerEventType.PetDown),
                Entry(InputAction.IaAction3, PointerEventType.PetUp),
            }));
        }

        [Test]
        public void SkipTheGlobalAppendOfAnEdgeThatNamedATargetEntity()
        {
            // A targeted edge is not a broadcast. ProcessPointerEventsSystem delivers it to that entity or to
            // nobody, so appending it here would leave the scene root observing a press the driver was told
            // missed — the same "reported miss mutated the scene" the entity filter exists to end.
            playerInteractionEntity.SyntheticPointerInput = new SyntheticPointerInput
            {
                PressButton = InputAction.IaPrimary,
                TargetWorld = world,
                TargetEntity = world.Create(),
                PostedAtFrame = UnityEngine.Time.frameCount,
            };

            system.Update(0);

            Assert.That(globalInputEvents.Entries, Is.Empty);
        }

        [Test]
        public void IgnoreStaleSyntheticPost()
        {
            PostSynthetic(InputAction.IaSecondary, null, frameOffset: -1);

            system.Update(0);

            Assert.That(globalInputEvents.Entries, Is.Empty);
        }

        [Test]
        public void ClearPreviousEntriesEveryFrame()
        {
            PostSynthetic(InputAction.IaSecondary, null);
            system.Update(0);
            Assert.That(globalInputEvents.Entries, Has.Count.EqualTo(1));

            // The post went stale (its frame passed); nothing may survive into the new frame's buffer.
            playerInteractionEntity.SyntheticPointerInput.PostedAtFrame = UnityEngine.Time.frameCount - 1;
            system.Update(0);

            Assert.That(globalInputEvents.Entries, Is.Empty);
        }

        [Test]
        public void SkipSyntheticEdgeWhenTheRealActionFiredTheSameFrame()
        {
            Press(keyboard.eKey);
            Assert.That(primaryAction.WasPressedThisFrame(), Is.True, "test precondition: the real press must be visible this frame");

            PostSynthetic(InputAction.IaPrimary, null);

            system.Update(0);

            // The real loop already added the primary press; the synthetic duplicate is skipped.
            Assert.That(globalInputEvents.Entries, Is.EqualTo(new[]
            {
                Entry(InputAction.IaPrimary, PointerEventType.PetDown),
            }));
        }

        [Test]
        public void LeaveTheSyntheticPostForDownstreamConsumers()
        {
            PostSynthetic(InputAction.IaSecondary, null);

            system.Update(0);

            // ProcessPointerEventsSystem owns the clearing; this system only reads the post.
            Assert.That(playerInteractionEntity.SyntheticPointerInput.PressButton, Is.EqualTo((InputAction?)InputAction.IaSecondary));
        }
    }
}
