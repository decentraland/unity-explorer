using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace DCL.SyntheticInput.UiSimulation
{
    /// <summary>
    ///     Semantic UI interaction: the target element is resolved first and its events are synthesized directly
    ///     (uGUI via ExecuteEvents, SDK scene UI via UI Toolkit SendEvent), after an occlusion pre-check so a
    ///     covered element cannot be clicked through its cover. Deterministic and cursor-independent; the
    ///     virtual-device gesture path exists for cases needing true positional fidelity.
    /// </summary>
    public class UiInteractionSimulator
    {
        /// <summary>Upper bound on the frames a (throttled) scene system may take to drain a delivered SDK pointer event.</summary>
        private const int MAX_SDK_DRAIN_FRAMES = 60;

        private readonly EventSystem eventSystem;
        private readonly PointerEventData pointerEventData;
        private readonly List<RaycastResult> raycastResults = new ();

        public UiInteractionSimulator(EventSystem eventSystem)
        {
            this.eventSystem = eventSystem;
            pointerEventData = new PointerEventData(eventSystem);
        }

        public UiActionResult ClickUgui(GameObject target, PointerEventData.InputButton button, bool force)
        {
            var rectTransform = (RectTransform)target.transform;
            Rect imageRect = UiScreenGeometry.ImageRectOf(rectTransform);

            if (!PrepareUguiPointer(target, UiScreenGeometry.ScreenCenterOf(rectTransform), force, out UiActionResult blockedResult))
                return blockedResult;

            pointerEventData.button = button;
            pointerEventData.clickCount = 1;

            GameObject pressRoot = raycastResults.Count > 0 ? raycastResults[0].gameObject : target;

            ExecuteEvents.Execute(target, pointerEventData, ExecuteEvents.pointerEnterHandler);

            GameObject? pressed = ExecuteEvents.ExecuteHierarchy(pressRoot, pointerEventData, ExecuteEvents.pointerDownHandler);
            pointerEventData.pointerPress = pressed;

            ExecuteEvents.Execute(pressed != null ? pressed : target, pointerEventData, ExecuteEvents.pointerUpHandler);

            GameObject? clickTarget = ExecuteEvents.GetEventHandler<IPointerClickHandler>(pressRoot);
            ExecuteEvents.Execute(clickTarget != null ? clickTarget : target, pointerEventData, ExecuteEvents.pointerClickHandler);

            ExecuteEvents.Execute(target, pointerEventData, ExecuteEvents.pointerExitHandler);

            return UiActionResult.Success(imageRect);
        }

        public UiActionResult SetTextUgui(GameObject target, string text, bool submit)
        {
            var field = target.GetComponentInChildren<TMP_InputField>();

            if (field == null)
                return UiActionResult.Failure("the element has no TMP_InputField");

            field.Select();
            field.ActivateInputField();
            field.text = text;

            if (submit)
                field.onSubmit.Invoke(field.text);

            field.DeactivateInputField();

            return UiActionResult.Success(UiScreenGeometry.ImageRectOf((RectTransform)field.transform));
        }

        public UiActionResult ScrollUgui(GameObject target, Vector2 delta, bool force)
        {
            var rectTransform = (RectTransform)target.transform;

            if (!PrepareUguiPointer(target, UiScreenGeometry.ScreenCenterOf(rectTransform), force, out UiActionResult blockedResult))
                return blockedResult;

            pointerEventData.scrollDelta = delta;

            GameObject scrollRoot = raycastResults.Count > 0 ? raycastResults[0].gameObject : target;
            ExecuteEvents.ExecuteHierarchy(scrollRoot, pointerEventData, ExecuteEvents.scrollHandler);

            return UiActionResult.Success(UiScreenGeometry.ImageRectOf(rectTransform));
        }

        /// <summary>
        ///     Clicks an SDK scene-UI element. Press and release are sent on separate frames — the scene's
        ///     pointer-event slot holds a single event, so a same-frame pair would lose the press — and the
        ///     release waits until the (throttled) scene system drained the press.
        /// </summary>
        public async UniTask<UiActionResult> ClickSdkAsync(SdkUiElement element, bool force, CancellationToken ct)
        {
            VisualElement target = element.Transform.Transform;

            if (target.panel == null)
                return UiActionResult.Failure("the element is not attached to a panel");

            Rect imageRect = UiScreenGeometry.PanelRectToImageRect(target.panel, target.worldBound);

            if (!force)
            {
                Vector2 panelCenter = target.worldBound.center;
                VisualElement picked = target.panel.Pick(panelCenter);

                if (picked == null || (picked != target && !target.Contains(picked)))
                    return UiActionResult.Failure(
                        "another element covers the target at its center",
                        picked != null ? $"{picked.name} ({string.Join(' ', picked.GetClasses())})" : "nothing pickable at the point",
                        imageRect);

                // A uGUI surface (e.g. a modal) raycast-hit at the same point sits on top of the scene UI panel.
                pointerEventData.Reset();
                pointerEventData.position = UiScreenGeometry.ImageToScreenPoint(UiScreenGeometry.PanelToImagePoint(target.panel, panelCenter));
                raycastResults.Clear();
                eventSystem.RaycastAll(pointerEventData, raycastResults);

                if (raycastResults.Count > 0)
                    return UiActionResult.Failure("a client UI element covers the scene UI at this point", PathOf(raycastResults[0].gameObject.transform), imageRect);
            }

            SendPooled<PointerEnterEvent>(target);
            SendPooled<PointerDownEvent>(target);

            var frames = 0;

            while (element.Transform.PointerEventTriggered != null && frames++ < MAX_SDK_DRAIN_FRAMES)
                await UniTask.Yield(PlayerLoopTiming.Update, ct);

            if (frames == 0)
                await UniTask.Yield(PlayerLoopTiming.Update, ct); // still split the legs across frames

            if (element.Transform.PointerEventTriggered != null)
                return UiActionResult.Failure("the scene did not consume the press (is the scene paused?); the release was not sent", null, imageRect);

            SendPooled<PointerUpEvent>(target);
            SendPooled<PointerLeaveEvent>(target);

            return UiActionResult.Success(imageRect);
        }

        public UiActionResult SetTextSdk(SdkUiElement element, string text, bool submit)
        {
            if (element.Input == null)
                return UiActionResult.Failure("the entity has no UiInput component");

            TextField textField = element.Input.TextField;

            if (textField.panel == null)
                return UiActionResult.Failure("the input is not attached to a panel");

            Rect imageRect = UiScreenGeometry.PanelRectToImageRect(textField.panel, textField.worldBound);

            textField.Focus();
            textField.value = text;

            if (submit)
            {
                using KeyDownEvent submitEvent = KeyDownEvent.GetPooled('\0', KeyCode.Return, EventModifiers.None);
                submitEvent.target = textField;
                textField.SendEvent(submitEvent);
            }

            textField.Blur();

            return UiActionResult.Success(imageRect);
        }

        public UiActionResult SelectDropdownSdk(SdkUiElement element, int index)
        {
            if (element.Dropdown == null)
                return UiActionResult.Failure("the entity has no UiDropdown component");

            DropdownField dropdownField = element.Dropdown.DropdownField;

            if (index < 0 || index >= dropdownField.choices.Count)
                return UiActionResult.Failure($"index {index} is out of range (the dropdown has {dropdownField.choices.Count} options)");

            dropdownField.index = index;

            return UiActionResult.Success(dropdownField.panel != null
                ? UiScreenGeometry.PanelRectToImageRect(dropdownField.panel, dropdownField.worldBound)
                : default(Rect));
        }

        public UiActionResult ScrollSdk(SdkUiElement element, Vector2 delta)
        {
            ScrollView? scrollView = element.Transform.InnerScrollView;

            if (scrollView == null)
                return UiActionResult.Failure("the entity has no scroll overflow");

            scrollView.scrollOffset += delta;

            return UiActionResult.Success(scrollView.panel != null
                ? UiScreenGeometry.PanelRectToImageRect(scrollView.panel, scrollView.worldBound)
                : default(Rect));
        }

        /// <summary>Fills the reusable pointer data at the point and runs the occlusion pre-check.</summary>
        private bool PrepareUguiPointer(GameObject target, Vector2 screenPoint, bool force, out UiActionResult blockedResult)
        {
            blockedResult = default(UiActionResult);

            pointerEventData.Reset();
            pointerEventData.position = screenPoint;
            pointerEventData.pressPosition = screenPoint;
            pointerEventData.scrollDelta = Vector2.zero;

            raycastResults.Clear();
            eventSystem.RaycastAll(pointerEventData, raycastResults);

            bool topHit = UiOcclusion.IsTopHitFor(target, raycastResults, out GameObject? blocker);

            if (!topHit && !force)
            {
                blockedResult = blocker != null
                    ? UiActionResult.Failure("another element covers the target at its center", PathOf(blocker.transform), UiScreenGeometry.ImageRectOf((RectTransform)target.transform))
                    : UiActionResult.Failure("nothing interactable raycasts at the target's center (is its raycastTarget off, or the element off-screen?)");

                return false;
            }

            if (raycastResults.Count > 0)
            {
                pointerEventData.pointerCurrentRaycast = raycastResults[0];
                pointerEventData.pointerPressRaycast = raycastResults[0];
            }

            return true;
        }

        private static void SendPooled<TEvent>(VisualElement target) where TEvent : EventBase<TEvent>, new()
        {
            using TEvent pooledEvent = EventBase<TEvent>.GetPooled();
            pooledEvent.target = target;
            target.SendEvent(pooledEvent);
        }

        /// <summary>Plain hierarchy path for diagnostics (no sibling indices — this string is not an address).</summary>
        private static string PathOf(Transform transform)
        {
            string path = transform.name;

            for (Transform? current = transform.parent; current != null; current = current.parent)
                path = $"{current.name}/{path}";

            return path;
        }
    }

    /// <summary>Driver-facing outcome of one semantic UI action.</summary>
    public struct UiActionResult
    {
        public bool Ok;
        public string? FailureReason;

        /// <summary>What covered the target, when the occlusion pre-check failed.</summary>
        public string? BlockedBy;

        /// <summary>The target's screen rect in image coordinates (top-left origin), when it could be computed.</summary>
        public Rect ScreenRect;

        public static UiActionResult Success(Rect screenRect) =>
            new () { Ok = true, ScreenRect = screenRect };

        public static UiActionResult Failure(string reason, string? blockedBy = null, Rect screenRect = default) =>
            new () { Ok = false, FailureReason = reason, BlockedBy = blockedBy, ScreenRect = screenRect };
    }
}
