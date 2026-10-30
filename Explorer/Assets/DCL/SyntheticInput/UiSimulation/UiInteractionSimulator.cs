using Cysharp.Threading.Tasks;
using DCL.SDKComponents.SceneUI.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

        // A result written at tick N reaches the scene in the batch of tick N or N + 1, so it has been read once the
        // tick number is N + 2.
        private const uint SCENE_TICKS_PER_EVENT = 2;
        private const float MAX_SCENE_TICK_WAIT_SEC = 2f;

        private readonly EventSystem eventSystem;
        private readonly SdkUiResolver sdkResolver;
        private readonly PointerEventData pointerEventData;

        // Kept apart from pointerEventData, which every other gesture resets while a hover stays parked.
        private readonly PointerEventData hoverEventData;
        private readonly List<RaycastResult> raycastResults = new ();

        // The slot of the last leave sent without waiting, settled by the next SDK gesture whichever element it targets.
        private UITransformComponent? unsettledSlot;

        public UiInteractionSimulator(EventSystem eventSystem, SdkUiResolver sdkResolver)
        {
            this.eventSystem = eventSystem;
            this.sdkResolver = sdkResolver;
            pointerEventData = new PointerEventData(eventSystem);
            hoverEventData = new PointerEventData(eventSystem);
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

            if (!TryCheckSdkPointer(target, force, "click", out UiActionResult blocked, out Rect imageRect))
                return blocked;

            if (!await DrainPendingSdkEventAsync(element.Transform, ct))
                return UiActionResult.Failure("the scene did not consume an earlier pointer event on the element (is the scene paused?); the click was not delivered", null, imageRect);

            SendPooled<PointerEnterEvent>(target);

            if (!await DrainSdkSlotAsync(element.Transform, ct))
                return UiActionResult.Failure("the scene did not consume the hover enter (is the scene paused?); the click was not delivered", null, imageRect);

            SendPooled<PointerDownEvent>(target);

            if (!await DrainSdkSlotAsync(element.Transform, ct))
                return UiActionResult.Failure("the scene did not consume the press (is the scene paused?); the release was not sent", null, imageRect);

            SendPooled<PointerUpEvent>(target);

            if (!await DrainSdkSlotAsync(element.Transform, ct))
                return UiActionResult.Failure("the scene did not consume the release (is the scene paused?)", null, imageRect);

            SendSdkLeave(target, element.Transform);

            return UiActionResult.Success(imageRect);
        }

        /// <summary>
        ///     Sends the enter and waits for the scene to consume it. The leave is <see cref="SendSdkLeave" />, sent by
        ///     whoever ends the hover.
        /// </summary>
        public async UniTask<(UiActionResult Result, bool EnterSent)> HoverSdkEnterAsync(SdkUiElement element, bool force, CancellationToken ct)
        {
            VisualElement target = element.Transform.Transform;

            if (!TryCheckSdkPointer(target, force, "hover", out UiActionResult blocked, out Rect imageRect))
                return (blocked, false);

            if (!await DrainPendingSdkEventAsync(element.Transform, ct))
                return (UiActionResult.Failure("the scene did not consume an earlier pointer event on the element (is the scene paused?); the hover was not delivered", null, imageRect), false);

            SendPooled<PointerEnterEvent>(target);

            // Not cancellable once the enter is sent, so the result always reports an enter that reached the element.
            if (!await DrainSdkSlotAsync(element.Transform, CancellationToken.None))
                return (UiActionResult.Failure("the scene did not consume the hover enter yet (is the scene paused?); the hover stays parked so it can be left", null, imageRect), true);

            return (UiActionResult.Success(imageRect), true);
        }

        /// <summary>Returns without waiting for the scene to consume the leave; the next SDK gesture waits for it.</summary>
        public void SendSdkLeave(VisualElement target, UITransformComponent? slot)
        {
            SendPooled<PointerLeaveEvent>(target);
            unsettledSlot = slot;
        }

        /// <summary>
        ///     Enters the top raycast hit (the target itself when forced past a cover) and each ancestor, as the input
        ///     module does for a pointer arriving from outside the canvas. The entered objects are appended to
        ///     <paramref name="enteredChain" /> in entry order.
        /// </summary>
        public UiActionResult HoverUguiEnter(GameObject target, bool force, List<GameObject> enteredChain)
        {
            var rectTransform = (RectTransform)target.transform;
            Rect imageRect = UiScreenGeometry.ImageRectOf(rectTransform);

            // A non-interactable element still raycasts, so a user's pointer enters it; only its press is ignored.
            if (!PrepareUguiPointer(target, UiScreenGeometry.ScreenCenterOf(rectTransform), force, out UiActionResult blockedResult, requireInteractable: false))
                return blockedResult;

            GameObject leaf = raycastResults.Count > 0 && UiOcclusion.IsTopHitFor(target, raycastResults, out _)
                ? raycastResults[0].gameObject
                : target;

            hoverEventData.Reset();
            hoverEventData.hovered.Clear();
            hoverEventData.position = pointerEventData.position;
            hoverEventData.pointerCurrentRaycast = pointerEventData.pointerCurrentRaycast;
            hoverEventData.pointerEnter = leaf;

            for (Transform? current = leaf.transform; current != null; current = current.parent)
            {
                GameObject entered = current.gameObject;
                hoverEventData.hovered.Add(entered);
                ExecuteEvents.Execute(entered, hoverEventData, ExecuteEvents.pointerEnterHandler);
                enteredChain.Add(entered);
            }

            UiActionResult result = UiActionResult.Success(imageRect);

            if (target.TryGetComponent(out Selectable selectable) && !selectable.IsInteractable())
                result.Info = "the element is not interactable: its handlers received the enter, but a disabled Selectable shows no highlight";

            return result;
        }

        /// <summary>Exits the chain leaf first, as the input module does; objects destroyed meanwhile are skipped.</summary>
        public void HoverUguiLeave(List<GameObject> enteredChain)
        {
            hoverEventData.Reset();

            foreach (GameObject entered in enteredChain)
            {
                if (entered == null)
                    continue;

                hoverEventData.hovered.Remove(entered);
                ExecuteEvents.Execute(entered, hoverEventData, ExecuteEvents.pointerExitHandler);
            }

            hoverEventData.hovered.Clear();
            hoverEventData.pointerEnter = null;
        }

        /// <summary>
        ///     Checks what a user's pointer at the element's center would meet: the element must be attached and enabled
        ///     and, unless <paramref name="force" />, be the top pick of its panel with no client UI above the panel.
        /// </summary>
        private bool TryCheckSdkPointer(VisualElement target, bool force, string gesture, out UiActionResult blocked, out Rect imageRect)
        {
            imageRect = default(Rect);

            if (target.panel == null)
            {
                blocked = UiActionResult.Failure("the element is not attached to a panel");
                return false;
            }

            imageRect = UiScreenGeometry.PanelRectToImageRect(target.panel, target.worldBound);

            // Before the pick and regardless of force: a disabled element is PickingMode.Ignore, so the pick would
            // blame a cover that is not there.
            if (!target.enabledInHierarchy)
            {
                blocked = UiActionResult.Failure($"the element is disabled; a user could not {gesture} it", null, imageRect);
                return false;
            }

            if (!force)
            {
                Vector2 panelCenter = target.worldBound.center;
                VisualElement picked = target.panel.Pick(panelCenter);

                if (picked == null || (picked != target && !target.Contains(picked)))
                {
                    blocked = UiActionResult.Failure(
                        "another element covers the target at its center",
                        picked != null ? $"{picked.name} ({string.Join(' ', picked.GetClasses())})" : "nothing pickable at the point",
                        imageRect);

                    return false;
                }

                pointerEventData.Reset();
                pointerEventData.position = UiScreenGeometry.ImageToScreenPoint(UiScreenGeometry.PanelToImagePoint(target.panel, panelCenter));
                raycastResults.Clear();
                eventSystem.RaycastAll(pointerEventData, raycastResults);

                if (TryFindClientCover(target.panel, out GameObject? cover))
                {
                    blocked = UiActionResult.Failure(
                        $"a client UI element covers the scene UI at this point (a client surface can be fully transparent and still take the {gesture}); pass force to {gesture} through it",
                        PathOf(cover.transform), imageRect);

                    return false;
                }
            }

            blocked = default(UiActionResult);
            return true;
        }

        /// <summary>
        ///     Settles the last leave sent without waiting, on any element, then the slot's own pending event. Returns
        ///     at once when neither is pending, so a gesture on an idle element starts without a frame of delay.
        /// </summary>
        private async UniTask<bool> DrainPendingSdkEventAsync(UITransformComponent? slotOwner, CancellationToken ct)
        {
            UITransformComponent? unsettled = unsettledSlot;
            unsettledSlot = null;

            // A detached element's slot is never consumed, and its leave reaches no handler.
            if (unsettled is { Transform: { panel: not null } } && !await DrainSdkSlotAsync(unsettled, ct))
                return false;

            return slotOwner == unsettled || slotOwner?.PointerEventTriggered == null || await DrainSdkSlotAsync(slotOwner, ct);
        }

        /// <summary>
        ///     The element's pointer-event slot holds one event, so the next event waits until the scene system
        ///     consumed the previous one and the scene has read it. A null owner has no slot to wait on.
        /// </summary>
        private async UniTask<bool> DrainSdkSlotAsync(UITransformComponent? slotOwner, CancellationToken ct)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, ct);

            if (slotOwner == null)
                return true;

            var frames = 0;

            while (slotOwner.PointerEventTriggered != null && frames++ < MAX_SDK_DRAIN_FRAMES)
                await UniTask.Yield(PlayerLoopTiming.Update, ct);

            return slotOwner.PointerEventTriggered == null && await WaitForSceneToReadAsync(ct);
        }

        /// <summary>
        ///     The scene reads the results written since its last tick as one batch and runs that batch's handlers in
        ///     registration order, not event order: a leave and the next enter in one batch reach the scene as enter,
        ///     then leave. Waits until the result written so far has been read, so the next one lands in a later batch.
        /// </summary>
        private async UniTask<bool> WaitForSceneToReadAsync(CancellationToken ct)
        {
            if (!sdkResolver.TryGetRunningSceneState(out ISceneStateProvider? sceneState))
                return true;

            uint readTick = sceneState.TickNumber + SCENE_TICKS_PER_EVENT;
            float deadline = UnityEngine.Time.realtimeSinceStartup + MAX_SCENE_TICK_WAIT_SEC;

            while (sceneState.TickNumber < readTick && UnityEngine.Time.realtimeSinceStartup < deadline)
                await UniTask.Yield(PlayerLoopTiming.Update, ct);

            return sceneState.TickNumber >= readTick;
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

            if (!await DrainPendingSdkEventAsync(pressSlot, ct))
                return UiActionResult.Failure("the scene did not consume an earlier pointer event on the start element (is the scene paused?); the drag was not delivered", null, imageRect);

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
                SendSdkLeave(target, slot);

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
        private bool TryFindClientCover(IPanel targetPanel, [NotNullWhen(true)] out GameObject? cover)
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

        private bool PrepareUguiPointer(GameObject target, Vector2 screenPoint, bool force, out UiActionResult blockedResult, bool requireInteractable = true)
        {
            blockedResult = default(UiActionResult);

            // Not skipped by force: a disabled Selectable drops the events and the click would report success.
            var selectable = target.GetComponent<Selectable>();

            if (requireInteractable && selectable != null && !selectable.IsInteractable())
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

        /// <summary>The address of a parked hover this gesture ended before it ran.</summary>
        public string? EndedHover;

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

            if (EndedHover != null)
                json["hoverEnded"] = EndedHover;

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
