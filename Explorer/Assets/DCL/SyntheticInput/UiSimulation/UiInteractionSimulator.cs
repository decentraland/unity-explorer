using Cysharp.Threading.Tasks;
using DCL.SDKComponents.SceneUI.Components;
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

            // Wheel units run opposite to image coordinates: a wheel-up (positive y) scrolls the content up.
            pointerEventData.scrollDelta = new Vector2(delta.x, -delta.y);

            GameObject scrollRoot = raycastResults.Count > 0 ? raycastResults[0].gameObject : target;
            ExecuteEvents.ExecuteHierarchy(scrollRoot, pointerEventData, ExecuteEvents.scrollHandler);

            return UiActionResult.Success(UiScreenGeometry.ImageRectOf(rectTransform));
        }

        /// <summary>
        ///     Clicks an SDK scene-UI element. Every pointer event is sent on its own frame and only after the
        ///     (throttled) scene system drained the previous one — the scene's pointer-event slot holds a single
        ///     event, so two events in one drain window lose the earlier one (a same-frame leave would eat the
        ///     release, a same-frame down would eat the hover enter).
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

                // A uGUI surface (e.g. a modal) raycast-hit above the scene UI panel covers it.
                pointerEventData.Reset();
                pointerEventData.position = UiScreenGeometry.ImageToScreenPoint(UiScreenGeometry.PanelToImagePoint(target.panel, panelCenter));
                raycastResults.Clear();
                eventSystem.RaycastAll(pointerEventData, raycastResults);

                if (TryFindClientCover(target.panel, out GameObject? cover))
                    return UiActionResult.Failure("a client UI element covers the scene UI at this point", PathOf(cover!.transform), imageRect);
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
        ///     Waits until the scene system drained the element's single pointer-event slot, always yielding at
        ///     least one frame so consecutive events land on separate frames. A null owner has no slot to wait on.
        ///     False when the slot still holds an event after the bounded wait.
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

        /// <summary>
        ///     Drags between two points inside the SDK scene-UI panel by synthesizing the element events a real
        ///     drag produces: press on the element under <paramref name="fromImagePoint" />, moves along the path,
        ///     release on the element under <paramref name="toImagePoint" />. This is the semantic counterpart of
        ///     the virtual-device drag — UI Toolkit panels consume events sent to their elements, and a scene
        ///     that recognises a drag as "down here, up there" observes exactly what a user produces.
        /// </summary>
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

            // One move per frame along the path: a scene reading the drag as a gesture sees it progress.
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

        /// <summary>
        ///     Delivers the release leg: the slot must be free before the up (a same-element drag may still hold
        ///     the press) and drained after it, so the leave sent last cannot overwrite the release the scene has
        ///     not read yet. On an unconsumed release the leave is withheld — the release stays in the slot for a
        ///     slow scene to pick up. True when the scene consumed the release.
        /// </summary>
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

        /// <summary>
        ///     Scrolls an SDK scroll container. <paramref name="delta" /> follows the layer's image-coordinate
        ///     convention (positive y scrolls the content down, toward later rows), which is also UI Toolkit's own
        ///     scroll-offset direction. The achieved offset is reported: a delta that only hits the clamp moves
        ///     nothing, and a silent success there is indistinguishable from a broken call.
        /// </summary>
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

        /// <summary>
        ///     Finds the client uGUI surface covering the scene UI at the already-raycast point, if any. The scene
        ///     UI panel is itself a uGUI raycast target — UI Toolkit registers a PanelRaycaster/PanelEventHandler
        ///     GameObject per PanelSettings — so its own hit is not a cover, and neither is anything the raycast
        ///     sorted <em>behind</em> it; only a surface above it can intercept the pointer.
        /// </summary>
        private bool TryFindClientCover(IPanel targetPanel, out GameObject? cover)
        {
            cover = null;

            if (raycastResults.Count == 0)
                return false;

            // Results are sorted front-most first, so only the top hit can intercept the pointer: the scene UI
            // panel's own hit means nothing is above it, and anything the raycast sorted behind it is irrelevant.
            GameObject topHit = raycastResults[0].gameObject;

            if (topHit.TryGetComponent(out PanelEventHandler handler) && ReferenceEquals(handler.panel, targetPanel))
                return false;

            cover = topHit;
            return true;
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

        /// <summary>What the action achieved, when a bare "ok" would hide it (e.g. a scroll that hit its clamp).</summary>
        public string? Info;

        /// <summary>What covered the target, when the occlusion pre-check failed.</summary>
        public string? BlockedBy;

        /// <summary>The target's screen rect in image coordinates (top-left origin), when it could be computed.</summary>
        public Rect ScreenRect;

        public static UiActionResult Success(Rect screenRect) =>
            new () { Ok = true, ScreenRect = screenRect };

        public static UiActionResult Failure(string reason, string? blockedBy = null, Rect screenRect = default) =>
            new () { Ok = false, FailureReason = reason, BlockedBy = blockedBy, ScreenRect = screenRect };

        /// <summary>The wire shape both driver front-ends (MCP tools, AltTester probes) hand back for a UI action.</summary>
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

            if (ScreenRect != default(Rect))
                json["screenRect"] = UiDiscovery.RectJson(ScreenRect);

            return json;
        }
    }
}
