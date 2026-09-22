using Cysharp.Threading.Tasks;
using DCL.SDKComponents.SceneUI.Components;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace DCL.SyntheticInput.UiSimulation
{
    /// <summary>
    ///     Semantic UI interaction: events are synthesized on the resolved element (uGUI via ExecuteEvents, scene UI
    ///     via UI Toolkit SendEvent) after an occlusion check, so a covered element is not clicked through its cover.
    /// </summary>
    public class UiInteractionSimulator
    {
        // The scene system is throttled, so one drain can take many frames.
        private const int MAX_SDK_DRAIN_FRAMES = 60;

        private readonly EventSystem eventSystem;
        private readonly SdkUiResolver sdkResolver;
        private readonly PointerEventData pointerEventData;
        private readonly List<RaycastResult> raycastResults = new ();

        public UiInteractionSimulator(EventSystem eventSystem, SdkUiResolver sdkResolver)
        {
            this.eventSystem = eventSystem;
            this.sdkResolver = sdkResolver;
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

            // Assigning field.text bypasses uGUI's interactable guard, so the guard is applied here.
            if (!field.IsInteractable())
                return UiActionResult.Failure("the input is not interactable (disabled by the client, or inside a CanvasGroup that is); a user could not type into it",
                    null, UiScreenGeometry.ImageRectOf((RectTransform)field.transform));

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

            // The image convention is positive y scrolls down. A uGUI wheel with positive y scrolls up, so negate.
            pointerEventData.scrollDelta = new Vector2(delta.x, -delta.y);

            GameObject scrollRoot = raycastResults.Count > 0 ? raycastResults[0].gameObject : target;
            ExecuteEvents.ExecuteHierarchy(scrollRoot, pointerEventData, ExecuteEvents.scrollHandler);

            return UiActionResult.Success(UiScreenGeometry.ImageRectOf(rectTransform));
        }

        public async UniTask<UiActionResult> ClickSdkAsync(SdkUiElement element, bool force, CancellationToken ct)
        {
            VisualElement target = element.Transform.Transform;

            if (target.panel == null)
                return UiActionResult.Failure("the element is not attached to a panel");

            Rect imageRect = UiScreenGeometry.PanelRectToImageRect(target.panel, target.worldBound);

            // Before the pick and regardless of force: a disabled element is PickingMode.Ignore, so the pick would
            // blame a cover that is not there.
            if (!target.enabledInHierarchy)
                return UiActionResult.Failure("the element is disabled; a user could not click it", null, imageRect);

            if (!force)
            {
                Vector2 panelCenter = target.worldBound.center;
                VisualElement picked = target.panel.Pick(panelCenter);

                if (picked == null || (picked != target && !target.Contains(picked)))
                    return UiActionResult.Failure(
                        "another element covers the target at its center",
                        picked != null ? $"{picked.name} ({string.Join(' ', picked.GetClasses())})" : "nothing pickable at the point",
                        imageRect);

                pointerEventData.Reset();
                pointerEventData.position = UiScreenGeometry.ImageToScreenPoint(UiScreenGeometry.PanelToImagePoint(target.panel, panelCenter));
                raycastResults.Clear();
                eventSystem.RaycastAll(pointerEventData, raycastResults);

                if (TryFindClientCover(target.panel, out GameObject? cover))
                    return UiActionResult.Failure(
                        "a client UI element covers the scene UI at this point (a client surface can be fully transparent and still take the click); pass force to click through it",
                        PathOf(cover!.transform), imageRect);
            }

            SendPooled<PointerEnterEvent>(target);

            if (!await DrainSdkSlotAsync(element.Transform, ct))
                return UiActionResult.Failure("the scene did not consume the hover enter (is the scene paused?); the click was not delivered", null, imageRect);

            SendPooled<PointerDownEvent>(target);

            if (!await DrainSdkSlotAsync(element.Transform, ct))
                return UiActionResult.Failure("the scene did not consume the press (is the scene paused?); the release was not sent", null, imageRect);

            SendPooled<PointerUpEvent>(target);

            if (!await DrainSdkSlotAsync(element.Transform, ct))
                return UiActionResult.Failure("the scene did not consume the release (is the scene paused?)", null, imageRect);

            SendPooled<PointerLeaveEvent>(target);

            return UiActionResult.Success(imageRect);
        }

        /// <summary>
        ///     The element's pointer-event slot holds one event, so the next event waits until the scene system
        ///     consumed the previous one. Always yields at least one frame, so consecutive events land on separate
        ///     frames. A null owner has no slot to wait on.
        /// </summary>
        private static async UniTask<bool> DrainSdkSlotAsync(UITransformComponent? slotOwner, CancellationToken ct)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, ct);

            if (slotOwner == null)
                return true;

            var frames = 0;

            while (slotOwner.PointerEventTriggered != null && frames++ < MAX_SDK_DRAIN_FRAMES)
                await UniTask.Yield(PlayerLoopTiming.Update, ct);

            return slotOwner.PointerEventTriggered == null;
        }

        public async UniTask<UiActionResult> DragSdkAsync(IPanel panel, Vector2 fromImagePoint, Vector2 toImagePoint, int steps, CancellationToken ct)
        {
            Vector2 fromPanelPoint = UiScreenGeometry.ImageToPanelPoint(panel, fromImagePoint);
            Vector2 toPanelPoint = UiScreenGeometry.ImageToPanelPoint(panel, toImagePoint);

            VisualElement? pressTarget = panel.Pick(fromPanelPoint);

            if (pressTarget == null)
                return UiActionResult.Failure("no scene UI element at the drag start point");

            Rect imageRect = UiScreenGeometry.PanelRectToImageRect(panel, pressTarget.worldBound);
            UITransformComponent? pressSlot = sdkResolver.ResolveComponent(pressTarget);

            SendPooled<PointerEnterEvent>(pressTarget);
            await DrainSdkSlotAsync(pressSlot, ct);
            SendPooled<PointerDownEvent>(pressTarget);

            if (!await DrainSdkSlotAsync(pressSlot, ct))
                return UiActionResult.Failure("the scene did not consume the press (is the scene paused?); the drag was abandoned", null, imageRect);

            // One move per frame, so the scene sees the drag progress.
            for (var step = 1; step <= steps; step++)
            {
                Vector2 pointOnPath = Vector2.Lerp(fromPanelPoint, toPanelPoint, step / (float)steps);
                VisualElement moveTarget = panel.Pick(pointOnPath) ?? pressTarget;

                SendPooled<PointerMoveEvent>(moveTarget);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            VisualElement? releaseTarget = panel.Pick(toPanelPoint);

            if (releaseTarget == null)
            {
                await ReleaseSdkAsync(pressTarget, pressSlot, ct);
                return UiActionResult.Failure($"no scene UI element at the drag end point; the release was delivered back to '{pressTarget.name}'", null, imageRect);
            }

            if (!await ReleaseSdkAsync(releaseTarget, sdkResolver.ResolveComponent(releaseTarget), ct))
                return UiActionResult.Failure("the scene did not consume the release (is the scene paused?)", null, imageRect);

            UiActionResult result = UiActionResult.Success(imageRect);
            result.Info = $"pressed '{DescribeElement(pressTarget)}', released '{DescribeElement(releaseTarget)}'";
            return result;
        }

        /// <summary>Drained after the up, so the leave cannot overwrite a release the scene has not read yet.</summary>
        private async UniTask<bool> ReleaseSdkAsync(VisualElement target, UITransformComponent? slot, CancellationToken ct)
        {
            await DrainSdkSlotAsync(slot, ct);
            SendPooled<PointerUpEvent>(target);

            bool consumed = await DrainSdkSlotAsync(slot, ct);

            if (consumed)
                SendPooled<PointerLeaveEvent>(target);

            return consumed;
        }

        private static string DescribeElement(VisualElement element) =>
            string.IsNullOrEmpty(element.name) ? element.GetType().Name : element.name;

        public UiActionResult SetTextSdk(SdkUiElement element, string text, bool submit)
        {
            if (element.Input == null)
                return UiActionResult.Failure("the entity has no UiInput component");

            TextField textField = element.Input.TextField;

            if (textField.panel == null)
                return UiActionResult.Failure("the input is not attached to a panel");

            Rect imageRect = UiScreenGeometry.PanelRectToImageRect(textField.panel, textField.worldBound);

            // Writing .value bypasses the disabled state, so it is checked here.
            if (!textField.enabledInHierarchy)
                return UiActionResult.Failure("the input is disabled (PBUiInput.disabled); a user could not type into it", null, imageRect);

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

            if (!dropdownField.enabledInHierarchy)
                return UiActionResult.Failure("the dropdown is disabled (PBUiDropdown.disabled); a user could not open it",
                    null, dropdownField.panel != null ? UiScreenGeometry.PanelRectToImageRect(dropdownField.panel, dropdownField.worldBound) : default(Rect));

            if (index < 0 || index >= dropdownField.choices.Count)
                return UiActionResult.Failure($"index {index} is out of range (the dropdown has {dropdownField.choices.Count} options)");

            dropdownField.index = index;

            return UiActionResult.Success(dropdownField.panel != null
                ? UiScreenGeometry.PanelRectToImageRect(dropdownField.panel, dropdownField.worldBound)
                : default(Rect));
        }

        /// <summary>Positive delta.y scrolls down, which matches UI Toolkit's scroll-offset direction.</summary>
        public UiActionResult ScrollSdk(SdkUiElement element, Vector2 delta)
        {
            ScrollView? scrollView = element.Transform.InnerScrollView;

            if (scrollView == null)
                return UiActionResult.Failure("the entity has no scroll overflow");

            Vector2 before = scrollView.scrollOffset;
            scrollView.scrollOffset = before + delta;
            Vector2 after = scrollView.scrollOffset;

            UiActionResult result = UiActionResult.Success(scrollView.panel != null
                ? UiScreenGeometry.PanelRectToImageRect(scrollView.panel, scrollView.worldBound)
                : default(Rect));

            result.Info = after == before
                ? $"the scroll offset did not move ({FormatOffset(before)}); the container is already at that end, or its content does not overflow"
                : $"scroll offset {FormatOffset(before)} -> {FormatOffset(after)}";

            return result;
        }

        private static string FormatOffset(Vector2 offset) =>
            $"({offset.x:F0}, {offset.y:F0})";

        /// <summary>The scene UI panel is itself a uGUI raycast target, so its own hit is not a cover.</summary>
        private bool TryFindClientCover(IPanel targetPanel, out GameObject? cover)
        {
            cover = null;

            if (raycastResults.Count == 0)
                return false;

            GameObject topHit = raycastResults[0].gameObject;

            if (topHit.TryGetComponent(out PanelEventHandler handler) && ReferenceEquals(handler.panel, targetPanel))
                return false;

            cover = topHit;
            return true;
        }

        private bool PrepareUguiPointer(GameObject target, Vector2 screenPoint, bool force, out UiActionResult blockedResult)
        {
            blockedResult = default(UiActionResult);

            // Not skipped by force: a disabled Selectable drops the events and the click would report success.
            var selectable = target.GetComponent<Selectable>();

            if (selectable != null && !selectable.IsInteractable())
            {
                blockedResult = UiActionResult.Failure("the element is not interactable (disabled by the client, or inside a CanvasGroup that is)",
                    null, UiScreenGeometry.ImageRectOf((RectTransform)target.transform));

                return false;
            }

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
                    ? UiActionResult.Failure("another element covers the target at its center (a cover can be fully transparent and still take the click); pass force to click through it",
                        PathOf(blocker.transform), UiScreenGeometry.ImageRectOf((RectTransform)target.transform))
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

        /// <summary>Diagnostics only: no sibling indices, so this string is not an address path.</summary>
        private static string PathOf(Transform transform)
        {
            string path = transform.name;

            for (Transform? current = transform.parent; current != null; current = current.parent)
                path = $"{current.name}/{path}";

            return path;
        }
    }

    public struct UiActionResult
    {
        public bool Ok;
        public string? FailureReason;

        /// <summary>Set when a bare ok would mislead, for example a scroll that hit its clamp.</summary>
        public string? Info;

        public string? BlockedBy;

        /// <summary>Image coordinates (top-left origin). Default when it could not be computed.</summary>
        public Rect ScreenRect;

        public static UiActionResult Success(Rect screenRect) =>
            new () { Ok = true, ScreenRect = screenRect };

        public static UiActionResult Failure(string reason, string? blockedBy = null, Rect screenRect = default) =>
            new () { Ok = false, FailureReason = reason, BlockedBy = blockedBy, ScreenRect = screenRect };

        public Newtonsoft.Json.Linq.JObject ToJson(string cursorState)
        {
            var json = new Newtonsoft.Json.Linq.JObject
            {
                ["ok"] = Ok,
                ["cursorState"] = cursorState,
            };

            if (FailureReason != null)
                json["reason"] = FailureReason;

            if (Info != null)
                json["info"] = Info;

            if (BlockedBy != null)
                json["blockedBy"] = BlockedBy;

            // Stated even without a rect: every coordinate in this payload is in this screen's space.
            json["screen"] = UiDiscovery.ScreenJson();

            if (ScreenRect != default(Rect))
            {
                json["screenRect"] = UiDiscovery.RectJson(ScreenRect);
                json["center"] = UiDiscovery.CenterJson(ScreenRect);
            }

            return json;
        }
    }
}
