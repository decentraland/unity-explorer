using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.PlayerOriginated.Utility;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using InputAction = UnityEngine.InputSystem.InputAction;
using RaycastHit = UnityEngine.RaycastHit;

namespace DCL.Interaction.PlayerOriginated.Tests
{
    public class InteractionInputUtilsShould : InputTestFixture
    {
        [Test]
        public void GatherAnyButtonReleased()
        {
            (Keyboard keyboard, InputAction[] actions) = CreateInput();

            // Can't release without pressing
            PressAndRelease(keyboard.cKey);

            InteractionInputUtils.AnyInputInfo anyInputInfo = actions.GatherAnyInputInfo();
            Assert.IsTrue(anyInputInfo.AnyButtonWasReleasedThisFrame);
        }

        [Test]
        public void GatherAnyButtonPressed()
        {
            (Keyboard keyboard, InputAction[] actions) = CreateInput();
            Press(keyboard.aKey);

            InteractionInputUtils.AnyInputInfo anyInputInfo = actions.GatherAnyInputInfo();
            Assert.IsTrue(anyInputInfo.AnyButtonIsPressed);
            Assert.IsFalse(anyInputInfo.AnyButtonWasReleasedThisFrame);
        }

        [Test]
        public void GatherAnyButtonPressedThisFrame()
        {
            (Keyboard keyboard, InputAction[] actions) = CreateInput();
            Press(keyboard.bKey);

            InteractionInputUtils.AnyInputInfo anyInputInfo = actions.GatherAnyInputInfo();
            Assert.IsTrue(anyInputInfo.AnyButtonWasPressedThisFrame);
            Assert.IsFalse(anyInputInfo.AnyButtonWasReleasedThisFrame);
        }

        [Test]
        public void GatherNoAnyInput()
        {
            (_, InputAction[] actions) = CreateInput();

            InteractionInputUtils.AnyInputInfo anyInputInfo = actions.GatherAnyInputInfo();
            Assert.IsFalse(anyInputInfo.AnyButtonWasPressedThisFrame);
            Assert.IsFalse(anyInputInfo.AnyButtonWasReleasedThisFrame);
            Assert.IsFalse(anyInputInfo.AnyButtonIsPressed);
        }

        [Test]
        public void QualifyByDistance()
        {
            Assert.IsTrue(InteractionInputUtils.IsQualifiedByDistance(new PlayerOriginRaycastResult(new RaycastHit { distance = 100 }), new PBPointerEvents.Types.Info { MaxDistance = 110 }));
        }

        [Test]
        public void AppendHoverInput()
        {
            var resultsIntent = new AppendPointerEventResultsIntent();

            var entry = new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetHoverEnter,
                EventInfo = new PBPointerEvents.Types.Info
                {
                    Button = ECSComponents.InputAction.IaPointer,
                    MaxDistance = 100,
                },
            };

            InteractionInputUtils.TryAppendHoverInput(ref resultsIntent, PointerEventType.PetHoverEnter, entry, 3);

            Assert.AreEqual(1, resultsIntent.ValidIndices.Length);
            Assert.AreEqual(3, resultsIntent.ValidIndices[0]);
        }

        [Test]
        public void NotAppendHoverInput()
        {
            var resultsIntent = new AppendPointerEventResultsIntent();

            var entry = new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetDown,
                EventInfo = new PBPointerEvents.Types.Info
                {
                    Button = ECSComponents.InputAction.IaAny,
                    MaxDistance = 100,
                },
            };

            InteractionInputUtils.TryAppendHoverInput(ref resultsIntent, PointerEventType.PetHoverEnter, entry, 3);

            Assert.AreEqual(0, resultsIntent.ValidIndices.Length);
        }

        [Test]
        public void AppendAnyButtonInput()
        {
            IReadOnlyDictionary<ECSComponents.InputAction, InputAction> map = Substitute.For<IReadOnlyDictionary<ECSComponents.InputAction, InputAction>>();

            var entry = new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetDown,
                EventInfo = new PBPointerEvents.Types.Info
                {
                    Button = ECSComponents.InputAction.IaAny,
                    MaxDistance = 100,
                },
            };

            var resultsIntent = new AppendPointerEventResultsIntent();

            InteractionInputUtils.TryAppendButtonLikeInput(map, entry, 2, ref resultsIntent, new InteractionInputUtils.AnyInputInfo(true, false, false));

            Assert.AreEqual(1, resultsIntent.ValidIndices.Length);
            Assert.AreEqual(2, resultsIntent.ValidIndices[0]);

            map.DidNotReceive().TryGetValue(Arg.Any<ECSComponents.InputAction>(), out Arg.Any<InputAction>());
        }

        [Test]
        public void AppendMappedButtonInput()
        {
            (Keyboard keyboard, InputAction[] actions) = CreateInput();

            IReadOnlyDictionary<ECSComponents.InputAction, InputAction> map = new Dictionary<ECSComponents.InputAction, InputAction>
            {
                { ECSComponents.InputAction.IaPointer, actions[0] },
                { ECSComponents.InputAction.IaAction3, actions[1] },
                { ECSComponents.InputAction.IaForward, actions[2] },
            };

            var entry = new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetDown,
                EventInfo = new PBPointerEvents.Types.Info
                {
                    Button = ECSComponents.InputAction.IaPointer,
                    MaxDistance = 100,
                },
            };

            Press(keyboard.aKey);

            var resultsIntent = new AppendPointerEventResultsIntent();
            InteractionInputUtils.TryAppendButtonLikeInput(map, entry, 0, ref resultsIntent, default(InteractionInputUtils.AnyInputInfo));

            Assert.AreEqual(1, resultsIntent.ValidIndices.Length);
            Assert.AreEqual(0, resultsIntent.ValidIndices[0]);

            entry = new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetUp,
                EventInfo = new PBPointerEvents.Types.Info
                {
                    Button = ECSComponents.InputAction.IaAction3,
                    MaxDistance = 100,
                },
            };

            PressAndRelease(keyboard.bKey);
            InteractionInputUtils.TryAppendButtonLikeInput(map, entry, 1, ref resultsIntent, default(InteractionInputUtils.AnyInputInfo));

            Assert.AreEqual(2, resultsIntent.ValidIndices.Length);
            Assert.AreEqual(1, resultsIntent.ValidIndices[1]);
        }

        private static (Keyboard, InputAction[]) CreateInput()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();

            var action1 = new InputAction("action1", InputActionType.Button, binding: "<Keyboard>/a");
            var action2 = new InputAction("action2", InputActionType.Button, binding: "<Keyboard>/b");
            var action3 = new InputAction("action3", InputActionType.Button, binding: "<Keyboard>/c");

            action1.Enable();
            action2.Enable();
            action3.Enable();

            return (keyboard, new[] { action1, action2, action3 });
        }
    }
}
