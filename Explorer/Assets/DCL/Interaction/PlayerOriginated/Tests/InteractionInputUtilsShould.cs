using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.PlayerOriginated.Utility;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            Assert.IsTrue(InteractionInputUtils.IsQualifiedByDistance(new PlayerOriginRaycastResultForSceneEntities(new RaycastHit { distance = 100 }), new PBPointerEvents.Types.Info { MaxDistance = 110 }));
        }

        [Test]
        public void AppendHoverInput()
        {
            var resultsIntent = new AppendPointerEventResultsIntent();
            resultsIntent.InitializeWithAlloc();

            var entry = new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetHoverEnter,
                EventInfo = new PBPointerEvents.Types.Info
                {
                    Button = ECSComponents.InputAction.IaPointer,
                    MaxDistance = 100,
                },
            };

            resultsIntent.AppendPointerInputIfQualified(PointerEventType.PetHoverEnter, entry, 3);

            Assert.AreEqual(1, resultsIntent.ValidIndicesCount());
            Assert.AreEqual(3, resultsIntent.ValidIndexAt(0));
        }

        [Test]
        public void NotAppendHoverInput()
        {
            var resultsIntent = new AppendPointerEventResultsIntent();
            resultsIntent.InitializeWithAlloc();

            var entry = new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetDown,
                EventInfo = new PBPointerEvents.Types.Info
                {
                    Button = ECSComponents.InputAction.IaAny,
                    MaxDistance = 100,
                },
            };

            resultsIntent.AppendPointerInputIfQualified(PointerEventType.PetHoverEnter, entry, 3);

            Assert.AreEqual(0, resultsIntent.ValidIndicesCount());
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

            resultsIntent.InitializeWithAlloc();
            InteractionInputUtils.TryAppendButtonLikeInput(map, entry, 2, ref resultsIntent, new InteractionInputUtils.AnyInputInfo(true, false, false));

            Assert.AreEqual(1, resultsIntent.ValidIndicesCount());
            Assert.AreEqual(2, resultsIntent.ValidIndexAt(0));

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
            resultsIntent.InitializeWithAlloc();
            InteractionInputUtils.TryAppendButtonLikeInput(map, entry, 0, ref resultsIntent, default(InteractionInputUtils.AnyInputInfo));

            Assert.AreEqual(1, resultsIntent.ValidIndicesCount());
            Assert.AreEqual(0, resultsIntent.ValidIndexAt(0));

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

            Assert.AreEqual(2, resultsIntent.ValidIndicesCount());
            Assert.AreEqual(1, resultsIntent.ValidIndexAt(1));
        }

        // This is a type-level optimization: InteractionInputUtils gained overloads whose parameters are the CONCRETE
        // Dictionary<,> / Dictionary<,>.ValueCollection instead of the IReadOnlyDictionary<,> / IEnumerable<> interfaces.
        // A `foreach` bound to the concrete type calls the collection's own GetEnumerator(), returning a struct enumerator
        // handled by value; bound to the interface it calls IEnumerable<>.GetEnumerator(), boxing that enumerator on the
        // heap once per call on the hot path. The non-boxing property is fully determined by the static parameter types,
        // so we verify it structurally by reflection.
        //
        // Why not measure allocation: GC.Alloc (ProfilerRecorder), Measure.Method().GC(), and
        // GC.GetAllocatedBytesForCurrentThread() all read unreliably (0 bytes, even on a known-boxing positive control)
        // in the headless -batchmode -nographics EditMode lane, so a signature check — no allocation, no graphics,
        // deterministic — is used instead.
        [Test]
        public void NotBoxEnumeratorsForConcreteDictionary()
        {
            const BindingFlags PUBLIC_STATIC = BindingFlags.Public | BindingFlags.Static;

            Type utils = typeof(InteractionInputUtils);
            Type concreteMap = typeof(Dictionary<ECSComponents.InputAction, InputAction>);
            Type valueCollection = typeof(Dictionary<ECSComponents.InputAction, InputAction>.ValueCollection);

            // GatherAnyInputInfo has an overload taking the concrete ValueCollection alongside the pre-existing
            // IEnumerable<InputAction> one.
            MethodInfo gatherConcrete = utils.GetMethods(PUBLIC_STATIC).SingleOrDefault(m =>
                m.Name == nameof(InteractionInputUtils.GatherAnyInputInfo)
                && m.GetParameters().Length == 1
                && m.GetParameters()[0].ParameterType == valueCollection);

            Assert.That(gatherConcrete, Is.Not.Null,
                "no GatherAnyInputInfo overload takes the concrete "
                + "Dictionary<,>.ValueCollection. Without it the call binds IEnumerable<InputAction> and "
                + "boxes IEnumerator<InputAction> on every hover frame.");

            Assert.That(gatherConcrete!.GetParameters()[0].ParameterType.IsInterface, Is.False,
                "GatherAnyInputInfo's non-boxing overload must take a concrete collection type, not an interface.");

            // TryAppendButtonAction has a (Dictionary<,>, ref intent) overload alongside the pre-existing
            // (IReadOnlyDictionary<,>, ref intent) one.
            MethodInfo appendConcrete = utils.GetMethods(PUBLIC_STATIC).SingleOrDefault(m =>
                m.Name == nameof(InteractionInputUtils.TryAppendButtonAction)
                && m.GetParameters().Length == 2
                && m.GetParameters()[0].ParameterType == concreteMap);

            Assert.That(appendConcrete, Is.Not.Null,
                "no TryAppendButtonAction(Dictionary<,>, ref ...) overload. Without it the "
                + "call binds IReadOnlyDictionary<,> and boxes the entry enumerator on every hover frame.");

            Type appendParam = appendConcrete!.GetParameters()[0].ParameterType;
            Assert.That(appendParam.IsInterface, Is.False,
                "TryAppendButtonAction's non-boxing overload must take the concrete Dictionary<,>, not IReadOnlyDictionary<,>.");
            Assert.That(appendParam, Is.EqualTo(concreteMap),
                "The non-boxing TryAppendButtonAction overload must bind the concrete Dictionary<,> map type.");

            // The concrete overloads are additive: the interface overloads they distinguish themselves
            // from must still exist, otherwise the "concrete vs interface" contract would be meaningless.
            Assert.That(
                utils.GetMethods(PUBLIC_STATIC).Any(m =>
                    m.Name == nameof(InteractionInputUtils.GatherAnyInputInfo)
                    && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType == typeof(IEnumerable<InputAction>)),
                Is.True, "Expected the interface GatherAnyInputInfo(IEnumerable<InputAction>) overload to remain.");

            // Mechanism check (documents WHY concrete typing avoids the allocation): the concrete
            // ValueCollection's own public GetEnumerator() returns a *value-type* enumerator, which a
            // concrete-typed foreach binds by value instead of boxing behind IEnumerator<InputAction>.
            MethodInfo structGetEnumerator = valueCollection.GetMethod(
                "GetEnumerator", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            Assert.That(structGetEnumerator, Is.Not.Null);
            Assert.That(structGetEnumerator!.ReturnType.IsValueType, Is.True,
                "Dictionary<,>.ValueCollection.GetEnumerator() must return a struct enumerator (bound by value = no boxing).");
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
