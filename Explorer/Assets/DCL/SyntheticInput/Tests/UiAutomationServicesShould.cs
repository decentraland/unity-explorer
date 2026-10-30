using Arch.Core;
using CRDT;
using Cysharp.Threading.Tasks;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SyntheticInput.UiSimulation;
using DCL.Utilities;
using ECS.SceneLifeCycle;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MouseButton = UnityEngine.InputSystem.LowLevel.MouseButton;

namespace DCL.SyntheticInput.Tests
{
    public class UiAutomationServicesShould
    {
        private World world = null!;
        private Entity playerEntity;
        private GameObject eventSystemGo = null!;
        private GameObject canvasRoot = null!;
        private Texture2D highlightTexture = null!;
        private Sprite highlightSprite = null!;
        private UiAutomationServices services = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            playerEntity = world.Create();

            eventSystemGo = new GameObject("test-event-system");
            var eventSystem = eventSystemGo.AddComponent<EventSystem>();

            var scenesCache = Substitute.For<IScenesCache>();
            scenesCache.CurrentScene.Returns(new ReactiveProperty<ISceneFacade?>(null));

            services = new UiAutomationServices(world, playerEntity, eventSystem, scenesCache);

            canvasRoot = new GameObject("Root", typeof(RectTransform));
            canvasRoot.AddComponent<Canvas>();

            highlightTexture = new Texture2D(2, 2);
            highlightSprite = Sprite.Create(highlightTexture, new Rect(0f, 0f, 2f, 2f), Vector2.zero);
        }

        [TearDown]
        public void TearDown()
        {
            services.Dispose();
            Object.DestroyImmediate(canvasRoot);
            Object.DestroyImmediate(highlightSprite);
            Object.DestroyImmediate(highlightTexture);
            Object.DestroyImmediate(eventSystemGo);
            World.Destroy(world);
        }

        /// <summary>
        ///     A sprite-swap Button shows the highlighted sprite exactly while a pointer is inside it, which makes the
        ///     enter and exit events observable without a test MonoBehaviour (one from an editor assembly cannot be added).
        /// </summary>
        private GameObject AddHighlightable(string name, GameObject parent)
        {
            var element = new GameObject(name, typeof(RectTransform));
            element.transform.SetParent(parent.transform);

            Image image = element.AddComponent<Image>();
            Button button = element.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { highlightedSprite = highlightSprite };

            return element;
        }

        private bool IsHighlighted(GameObject element) =>
            element.GetComponent<Image>().overrideSprite == highlightSprite;

        // EditMode raycasts reach no graphic, so the tests force past the occlusion pre-check to reach the events.
        private UiActionResult HoverForced(GameObject target, string path) =>
            services.HoverUguiAsync(UiElementAddress.UguiPath(path), target, force: true, device: false, timeoutSec: 1f, CancellationToken.None)
                    .GetAwaiter().GetResult().Result;

        [Test]
        public void EnterTheHoveredElementAndItsAncestorsAndKeepTheHoverParked()
        {
            GameObject panel = AddHighlightable("Panel", canvasRoot);
            GameObject button = AddHighlightable("Button", panel);

            UiActionResult result = HoverForced(button, "Root/Panel/Button");

            Assert.That(result.Ok, Is.True);
            Assert.That(IsHighlighted(button), Is.True);
            Assert.That(IsHighlighted(panel), Is.True, "the input module enters every ancestor of the hovered element");
            Assert.That(services.HoveredAddress, Is.EqualTo("ugui:path=Root/Panel/Button"), "the hover rests until something ends it");
        }

        [Test]
        public void ExitEveryEnteredObjectWhenTheHoverEnds()
        {
            GameObject panel = AddHighlightable("Panel", canvasRoot);
            GameObject button = AddHighlightable("Button", panel);
            HoverForced(button, "Root/Panel/Button");

            string? ended = services.EndHover();

            Assert.That(ended, Is.EqualTo("ugui:path=Root/Panel/Button"));
            Assert.That(IsHighlighted(button), Is.False);
            Assert.That(IsHighlighted(panel), Is.False);
            Assert.That(services.HoveredAddress, Is.Null);
        }

        [Test]
        public void LeaveThePreviousHoverBeforeEnteringTheNextOne()
        {
            GameObject first = AddHighlightable("First", canvasRoot);
            GameObject second = AddHighlightable("Second", canvasRoot);
            HoverForced(first, "Root/First");

            UiActionResult result = HoverForced(second, "Root/Second");

            Assert.That(result.EndedHover, Is.EqualTo("ugui:path=Root/First"));
            Assert.That(result.ToJson("Free")["hoverEnded"]?.ToString(), Is.EqualTo("ugui:path=Root/First"));
            Assert.That(IsHighlighted(first), Is.False);
            Assert.That(IsHighlighted(second), Is.True);
            Assert.That(services.HoveredAddress, Is.EqualTo("ugui:path=Root/Second"));
        }

        [Test]
        public void ReportNothingEndedWhenNoHoverIsParked()
        {
            Assert.That(services.EndHover(), Is.Null);
            Assert.That(services.HoveredAddress, Is.Null);
        }

        [Test]
        public void SkipAHoveredObjectDestroyedWhileTheHoverWasParked()
        {
            GameObject panel = AddHighlightable("Panel", canvasRoot);
            GameObject button = AddHighlightable("Button", panel);
            HoverForced(button, "Root/Panel/Button");

            Object.DestroyImmediate(button);

            Assert.That(services.EndHover(), Is.EqualTo("ugui:path=Root/Panel/Button"));
            Assert.That(IsHighlighted(panel), Is.False, "the surviving ancestors still receive their exit");
        }

        [Test]
        public void HoverANonInteractableElementAndSayItShowsNoHighlight()
        {
            GameObject element = AddHighlightable("DisabledButton", canvasRoot);
            element.GetComponent<Button>().interactable = false;

            UiActionResult result = HoverForced(element, "Root/DisabledButton");

            Assert.That(result.Ok, Is.True, "a disabled element still raycasts, so a user's pointer enters it");
            Assert.That(result.Info, Does.Contain("not interactable"));
            Assert.That(services.HoveredAddress, Is.EqualTo("ugui:path=Root/DisabledButton"));
        }

        [Test]
        public void ExplainWhyASceneUiDragDoesNotApply()
        {
            UniTask<SceneUiDragAttempt> drag = services.DragSceneUiAsync(new Vector2(10f, 10f), new Vector2(20f, 20f), steps: 4, CancellationToken.None);

            Assert.That(drag.Status, Is.EqualTo(UniTaskStatus.Succeeded));

            SceneUiDragAttempt attempt = drag.GetAwaiter().GetResult();
            Assert.That(attempt.Result, Is.Null);
            Assert.That(attempt.SkipReason, Is.EqualTo("no running current scene"));
        }

        [Test]
        public void RefuseADeviceClickOnADetachedSdkElementWithoutQueuingAGesture()
        {
            var transform = new UITransformComponent();
            transform.InitializeAsChild("Detached", new CRDTEntity(600), new CRDTEntity(0));

            UniTask<UiActionResult> click = services.ClickSdkWithDevicesAsync(new SdkUiElement(transform, null, null), MouseButton.Left, force: false, timeoutSec: 1f, CancellationToken.None);

            Assert.That(click.Status, Is.EqualTo(UniTaskStatus.Succeeded), "the pre-check refuses before any device state is queued");
            Assert.That(click.GetAwaiter().GetResult().Ok, Is.False);
            Assert.That(world.Has<UiDeviceGestureRequest>(playerEntity), Is.False);
        }

        [Test]
        public void ReportNoUiCoverWhenNothingIsOnScreen()
        {
            Assert.That(services.TryFindUiCoverAt(new Vector2(5f, 5f), out string cover), Is.False);
            Assert.That(cover, Is.Empty);
        }

        [Test]
        public void NoteThatNoUiReceivedADragOverTheWorld()
        {
            UiDeviceDragOutcome outcome = UiDeviceDragOutcome.From(new UiGestureResult { Ok = true }, null, null);

            Assert.That(outcome.Ok, Is.True);
            Assert.That(outcome.DeliveryNote, Does.Contain("no UI element received this drag"));
            Assert.That(outcome.DeliveryNote, Does.Contain("sweep_pointer"));
        }

        [Test]
        public void AddNoNoteWhenTheDragStartedOnUi()
        {
            UiDeviceDragOutcome outcome = UiDeviceDragOutcome.From(new UiGestureResult { Ok = true }, "MainUI/Sidebar/ExploreButton", null);

            Assert.That(outcome.CoverAtStart, Is.EqualTo("MainUI/Sidebar/ExploreButton"));
            Assert.That(outcome.CoverAtEnd, Is.Null);
            Assert.That(outcome.DeliveryNote, Is.Null);
        }

        [Test]
        public void AddNoNoteToAFailedDrag()
        {
            UiDeviceDragOutcome outcome = UiDeviceDragOutcome.From(
                new UiGestureResult { Ok = false, FailureReason = "the drag panned the camera instead of dragging" }, null, null);

            Assert.That(outcome.Ok, Is.False);
            Assert.That(outcome.FailureReason, Does.Contain("panned the camera"));
            Assert.That(outcome.DeliveryNote, Is.Null);
        }
    }
}
