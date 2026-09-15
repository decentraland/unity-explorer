using DCL.SyntheticInput.UiSimulation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

namespace DCL.SyntheticInput.Tests
{
    /// <summary>
    ///     The pre-checks that decide whether an action is delivered at all. The event synthesis itself is verified
    ///     end-to-end against a running client; what is pinned here is that the simulator refuses what a user could
    ///     not do, instead of reporting a success for an action the UI would have ignored.
    /// </summary>
    public class UiInteractionSimulatorShould
    {
        private GameObject eventSystemGo = null!;
        private GameObject canvasGo = null!;
        private UiInteractionSimulator simulator = null!;

        [SetUp]
        public void SetUp()
        {
            eventSystemGo = new GameObject("test-event-system");
            var eventSystem = eventSystemGo.AddComponent<EventSystem>();

            canvasGo = new GameObject("TestCanvasRoot", typeof(RectTransform));
            canvasGo.AddComponent<Canvas>();

            simulator = new UiInteractionSimulator(eventSystem, new SdkUiResolver(NSubstitute.Substitute.For<ECS.SceneLifeCycle.IScenesCache>()));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(canvasGo);
            Object.DestroyImmediate(eventSystemGo);
        }

        private GameObject AddElement(string name)
        {
            var element = new GameObject(name, typeof(RectTransform));
            element.transform.SetParent(canvasGo.transform);
            element.AddComponent<Image>();
            return element;
        }

        [Test]
        public void RefuseToClickADisabledSelectable()
        {
            GameObject element = AddElement("DisabledButton");
            Button button = element.AddComponent<Button>();
            button.interactable = false;

            UiActionResult result = simulator.ClickUgui(element, PointerEventData.InputButton.Left, force: false);

            Assert.That(result.Ok, Is.False, "a disabled button ignores the events, so reporting a delivered click would be a lie");
            Assert.That(result.FailureReason, Does.Contain("not interactable"));
        }

        [Test]
        public void RefuseToClickThroughAnAncestorCanvasGroupThatDisablesTheSubtree()
        {
            GameObject element = AddElement("GroupedButton");
            element.AddComponent<Button>();

            CanvasGroup group = canvasGo.AddComponent<CanvasGroup>();
            group.interactable = false;

            UiActionResult result = simulator.ClickUgui(element, PointerEventData.InputButton.Left, force: false);

            Assert.That(result.Ok, Is.False);
            Assert.That(result.FailureReason, Does.Contain("CanvasGroup"));
        }

        [Test]
        public void RefuseToWriteIntoANonInteractableInputField()
        {
            GameObject element = AddElement("DisabledInput");
            TMP_InputField field = element.AddComponent<TMP_InputField>();
            field.interactable = false;

            UiActionResult result = simulator.SetTextUgui(element, "should not land", submit: false);

            Assert.That(result.Ok, Is.False, "assigning .text bypasses uGUI's own guard, so the guard has to be applied here");
            Assert.That(result.FailureReason, Does.Contain("not interactable"));
            Assert.That(field.text, Is.Empty, "the write must not have happened");
        }

        [Test]
        public void StateTheScreenOnEveryResultEvenWithoutARect()
        {
            // The device path resolves no rect, but the frame of reference is still knowable — and every caller
            // normalizes coordinates against it.
            JObject json = UiActionResult.Success(default(Rect)).ToJson("Free");

            Assert.That(json["screen"], Is.Not.Null);
            Assert.That(json["screen"]!["width"]!.Value<int>(), Is.EqualTo(Screen.width));
            Assert.That(json["screenRect"], Is.Null, "no rect was resolved, so none is claimed");
        }

        [Test]
        public void StateTheRectAndItsCenterWhenAnElementWasResolved()
        {
            JObject json = UiActionResult.Success(new Rect(10f, 20f, 30f, 40f)).ToJson("Free");

            Assert.That(json["screenRect"], Is.Not.Null);
            Assert.That(json["center"], Is.Not.Null);
            Assert.That(json["screen"], Is.Not.Null);
        }
    }
}
