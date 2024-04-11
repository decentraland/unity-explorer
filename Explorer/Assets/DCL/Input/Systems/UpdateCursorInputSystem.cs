using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.CharacterCamera.Systems;
using DCL.Diagnostics;
using DCL.Input.Crosshair;
using DCL.Interaction.PlayerOriginated.Components;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;
using Toggle = UnityEngine.UIElements.Toggle;

namespace DCL.Input.Systems
{
    [UpdateInGroup(typeof(InputGroup))]
    [UpdateBefore(typeof(UpdateCameraInputSystem))]
    [LogCategory(ReportCategory.INPUT)]
    public partial class UpdateCursorInputSystem : UpdateInputSystem<CameraInput, CameraComponent>
    {
        private const int MOUSE_BOUNDS_OFFSET = 10;
        private static readonly Vector2 CURSOR_OFFSET = new (20, -20);

        private readonly IEventSystem eventSystem;
        private readonly ICursor cursor;
        private readonly ICrosshairView crosshairCanvas;
        private readonly DCLInput.CameraActions cameraActions;
        private readonly DCLInput.UIActions uiActions;
        private bool hasHoverCollider;
        private bool isAtDistance;
        private bool isHoveringAnInteractable;
        private readonly Dictionary<GameObject, bool> interactionCache = new ();
        private readonly Dictionary<GameObject, PanelEventHandler> uiToolkitPanel = new ();
        private readonly Dictionary<VisualElement, bool> uiToolkitInteractionCache = new ();
        private readonly List<VisualElement> visualElementPickCache = new ();
        private readonly Mouse mouseDevice;

        internal UpdateCursorInputSystem(World world, DCLInput dclInput, IEventSystem eventSystem, ICursor cursor, ICrosshairView crosshairCanvas) : base(world)
        {
            this.eventSystem = eventSystem;
            this.cursor = cursor;
            this.crosshairCanvas = crosshairCanvas;
            cameraActions = dclInput.Camera;
            uiActions = dclInput.UI;
            mouseDevice = InputSystem.GetDevice<Mouse>();
        }

        protected override void Update(float t)
        {
            GetSDKInteractionStateQuery(World);
            UpdateCursorQuery(World);
        }

        [Query]
        private void GetSDKInteractionState(in HoverStateComponent hoverStateComponent)
        {
            hasHoverCollider = hoverStateComponent.HasCollider;
            isAtDistance = hoverStateComponent.IsAtDistance;
            isHoveringAnInteractable = hasHoverCollider && isAtDistance;
        }

        [Query]
        private void UpdateCursor(ref CursorComponent cursorComponent)
        {
            Vector2 mousePos = mouseDevice.position.value;
            Vector2 controllerDelta = uiActions.ControllerDelta.ReadValue<Vector2>();
            IReadOnlyList<RaycastResult> raycastResults = eventSystem.RaycastAll(mousePos);
            cursorComponent.IsOverUI = eventSystem.IsPointerOverGameObject();

            UpdateCursorLockState(ref cursorComponent, mousePos, raycastResults);
            UpdateCursorVisualState(ref cursorComponent, raycastResults);
            UpdateCursorPosition(ref cursorComponent, controllerDelta, mousePos);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateCursorVisualState(ref CursorComponent cursorComponent, IReadOnlyList<RaycastResult> raycastResults)
        {
            CursorStyle cursorStyle = CursorStyle.Normal;

            switch (cursorComponent.CursorState)
            {
                case CursorState.Free:
                {
                    if (isHoveringAnInteractable)
                        cursorStyle = CursorStyle.Interaction;

                    for (var i = 0; i < raycastResults.Count; i++)
                    {
                        GameObject? obj = raycastResults[i].gameObject;

                        if (obj == null)
                            continue;

                        bool isInteractable = IsInteractableCached(obj, cursorComponent.Position);

                        if (!isInteractable)
                            continue;

                        cursorStyle = CursorStyle.Interaction;
                        break;
                    }

                    break;
                }
                case CursorState.Panning:
                    cursorStyle = CursorStyle.CameraPan;
                    break;
            }

            cursor.SetStyle(cursorStyle);
            crosshairCanvas.SetCursorStyle(cursorStyle);
        }

        // We check if the gameObject is interactable or not, at least once.
        // For UI Elements we do a PickAll and check its results by using the same logic
        // we have to check if we can avoid doing a PickAll every frame, it seems that its not slow at least
        private bool IsInteractableCached(GameObject gameObject, Vector2 pointerPosition)
        {
            if (uiToolkitPanel.TryGetValue(gameObject, out PanelEventHandler? panelEventHandler))
            {
                // we need to convert screen coord to panel coord, since uiElement panel anchor is top-left coordinate we flip the y axis
                Vector2 localCoord = pointerPosition;
                localCoord.y = Screen.height - pointerPosition.y;
                localCoord = RuntimePanelUtils.ScreenToPanel(panelEventHandler.panel, localCoord);

                panelEventHandler.panel.PickAll(localCoord, visualElementPickCache);

                for (var i = 0; i < visualElementPickCache.Count; i++)
                {
                    VisualElement? visualElement = visualElementPickCache[i];

                    if (uiToolkitInteractionCache.TryGetValue(visualElement, out bool isInteractable))
                    {
                        if (isInteractable)
                            return true;

                        continue;
                    }

                    bool canBeInteracted = visualElement is Button or Toggle;
                    uiToolkitInteractionCache.Add(visualElement, canBeInteracted);

                    if (canBeInteracted)
                        return true;
                }

                return false;
            }

            if (interactionCache.TryGetValue(gameObject, out bool result))
                return result;

            // In theory Selectable should cover UnityEngine.UI.Toggle but it does not, weird
            Selectable? isCanvasButton = gameObject.GetComponent<Selectable>();

            if (isCanvasButton)
            {
                interactionCache.Add(gameObject, true);
                return true;
            }

            PanelEventHandler? eventHandler = gameObject.GetComponent<PanelEventHandler>();

            if (eventHandler)
                uiToolkitPanel.Add(gameObject, eventHandler);

            interactionCache.Add(gameObject, false);
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateCursorLockState(ref CursorComponent cursorComponent, Vector2 mousePos, IReadOnlyList<RaycastResult> raycastResults)
        {
            CursorState nextState = cursorComponent.CursorState;

            if (cursorComponent is { IsOverUI: true, CursorState: CursorState.Locked })
                nextState = CursorState.Free;

            bool isMouseOutOfBounds = mousePos.x < MOUSE_BOUNDS_OFFSET || mousePos.x > Screen.width - MOUSE_BOUNDS_OFFSET ||
                                      mousePos.y < MOUSE_BOUNDS_OFFSET || mousePos.y > Screen.height - MOUSE_BOUNDS_OFFSET;

            bool inputWantsToLock = cameraActions.Lock.WasReleasedThisFrame();
            bool inputWantsToUnlock = cameraActions.Unlock.WasReleasedThisFrame();
            bool isTemporalLock = cameraActions.TemporalLock.IsPressed();

            if (!isMouseOutOfBounds && inputWantsToLock && cursorComponent is { CursorState: CursorState.Free, PositionIsDirty: false, IsOverUI: false })
            {
                if (raycastResults.Count == 0 && !isHoveringAnInteractable)
                {
                    nextState = CursorState.Locked;
                }
            }
            else if (inputWantsToUnlock && cursorComponent is { CursorState: CursorState.Locked })
                nextState = CursorState.Free;

            // in case the cursor was unlocked externally
            if (!cursor.IsLocked() && cursorComponent is { CursorState: CursorState.Locked })
                nextState = CursorState.Free;

            if (!isMouseOutOfBounds && isTemporalLock && cursorComponent is { CursorState: CursorState.Free, PositionIsDirty: true, IsOverUI: false })
                nextState = CursorState.Panning;

            if (!isTemporalLock && cursorComponent is { CursorState: CursorState.Panning })
                nextState = CursorState.Free;

            UpdateState(ref cursorComponent, nextState);

            if (cursorComponent.CursorState != CursorState.Panning)
                cursorComponent.PositionIsDirty = false;
        }

        private void UpdateState(ref CursorComponent cursorComponent, CursorState nextState)
        {
            if (cursorComponent.CursorState == nextState) return;

            switch (nextState)
            {
                case CursorState.Free:
                    crosshairCanvas.SetDisplayed(false);
                    cursor.SetVisibility(true);
                    cursor.Unlock();
                    break;

                case CursorState.Locked:
                    crosshairCanvas.SetDisplayed(true);
                    cursor.Lock();
                    cursor.SetVisibility(false);
                    break;

                case CursorState.Panning:
                    crosshairCanvas.SetDisplayed(true);
                    cursor.SetVisibility(false);
                    break;
            }

            cursorComponent.CursorState = nextState;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateCursorPosition(ref CursorComponent cursorComponent, Vector2 controllerDelta, Vector2 mousePos)
        {
            bool isPanning = cursorComponent.CursorState == CursorState.Panning;

            if (isPanning)
            {
                Vector2 pos = cursorComponent.Position + CURSOR_OFFSET;
                pos.x = pos.x / Screen.width * 100f;
                pos.y = pos.y / Screen.height * 100f;
                crosshairCanvas.SetPosition(pos);
                Mouse.current.WarpCursorPosition(cursorComponent.Position);
            }
            else
                crosshairCanvas.ResetPosition();

            if (isPanning)
                return;

            if (controllerDelta.sqrMagnitude > 0)
            {
                // If we unlock for the first time we update the mouse position
                if (Mathf.Approximately(cursorComponent.Position.x, 0) &&
                    Mathf.Approximately(cursorComponent.Position.y, 0))
                    cursorComponent.Position = mousePos;

                // Todo: extract the +1 to sensitivity settings for controllers
                float fastCursor = uiActions.ControllerFastCursor.ReadValue<float>() + 1;
                cursorComponent.Position += controllerDelta * fastCursor;
                Mouse.current.WarpCursorPosition(cursorComponent.Position);
            }
            else
                cursorComponent.Position = mousePos;
        }
    }
}
