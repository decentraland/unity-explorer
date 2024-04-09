using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.CharacterCamera.Systems;
using DCL.Diagnostics;
using DCL.Input.Crosshair;
using DCL.Interaction.PlayerOriginated.Components;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Utility.UIToolkit;
using Button = UnityEngine.UIElements.Button;
using Toggle = UnityEngine.UIElements.Toggle;

namespace DCL.Input.Systems
{
    [UpdateInGroup(typeof(InputGroup))]
    [UpdateBefore(typeof(UpdateCameraInputSystem))]
    [LogCategory(ReportCategory.INPUT)]
    public partial class UpdateCursorInputSystem : UpdateInputSystem<CameraInput, CameraComponent>
    {
        private readonly IEventSystem eventSystem;
        private readonly ICursor cursor;
        private readonly CrosshairCanvas crosshairCanvas;
        private readonly DCLInput.CameraActions cameraActions;
        private readonly DCLInput.UIActions uiActions;
        private bool hasHoverCollider;
        private bool isAtDistance;
        private bool isHoveringAnInteractable;
        private readonly Dictionary<GameObject, bool> interactionCache = new ();
        private readonly Dictionary<GameObject, PanelEventHandler> uiToolkitPanel = new ();
        private readonly Dictionary<VisualElement, bool> uiToolkitInteractionCache = new ();
        private readonly List<VisualElement> visualElementPickCache = new ();

        internal UpdateCursorInputSystem(World world, DCLInput dclInput, IEventSystem eventSystem, ICursor cursor, CrosshairCanvas crosshairCanvas) : base(world)
        {
            this.eventSystem = eventSystem;
            this.cursor = cursor;
            this.crosshairCanvas = crosshairCanvas;
            cameraActions = dclInput.Camera;
            uiActions = dclInput.UI;
        }

        protected override void Update(float t)
        {
            GetSDKInteractionStateQuery(World);
            UpdateCursorQuery(World);
        }

        [Query]
        private void GetSDKInteractionState(in HoverStateComponent hoverStateComponent)
        {
            hasHoverCollider = hoverStateComponent.LastHitCollider != null;
            isAtDistance = hoverStateComponent.IsAtDistance;
            isHoveringAnInteractable = hasHoverCollider && isAtDistance;
        }

        [Query]
        private void UpdateCursor(ref CursorComponent cursorComponent)
        {
            Vector2 mousePos = Mouse.current.position.value;
            Vector2 controllerDelta = uiActions.ControllerDelta.ReadValue<Vector2>();
            IReadOnlyList<RaycastResult> raycastResults = eventSystem.RaycastAll(mousePos);

            UpdateCursorPosition(ref cursorComponent, controllerDelta, mousePos);
            UpdateCursorLockState(ref cursorComponent, mousePos, raycastResults);
            UpdateCursorVisualState(ref cursorComponent, raycastResults);

            crosshairCanvas.SetDisplayed(cursorComponent.CursorIsLocked);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateCursorVisualState(ref CursorComponent cursorComponent, IReadOnlyList<RaycastResult> raycastResults)
        {
            CursorStyle cursorStyle = CursorStyle.Normal;
            cursorComponent.IsOverUI = eventSystem.IsPointerOverGameObject();

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
            if (cursorComponent.IsOverUI)
            {
                if (cursorComponent.CursorIsLocked)
                    UnlockCursor(ref cursorComponent);

                cursorComponent.AllowCameraMovement = false;
                return;
            }

            var mouseBoundsOffset = 10;

            bool isMouseOutOfBounds = mousePos.x < mouseBoundsOffset || mousePos.x > Screen.width - mouseBoundsOffset ||
                                      mousePos.y < mouseBoundsOffset || mousePos.y > Screen.height - mouseBoundsOffset;

            bool inputWantsToLock = cameraActions.Lock.WasReleasedThisFrame();
            bool inputWantsToUnlock = cameraActions.Unlock.WasReleasedThisFrame();
            bool justStoppedTemporalLock = cameraActions.TemporalLock.WasReleasedThisFrame();

            if (inputWantsToLock && !isMouseOutOfBounds && cursorComponent is { CursorIsLocked: false, CancelCursorLock: false })
            {
                if (raycastResults.Count == 0 && !isHoveringAnInteractable)
                {
                    cursorComponent.CursorIsLocked = true;
                    cursor.Lock();
                }
            }
            else if (inputWantsToUnlock && cursorComponent.CursorIsLocked)
                UnlockCursor(ref cursorComponent);

            // in case the cursor was unlocked externally
            if (!cursor.IsLocked() && cursorComponent.CursorIsLocked)
                UnlockCursor(ref cursorComponent);

            cursorComponent.AllowCameraMovement = !isMouseOutOfBounds && (cameraActions.TemporalLock.IsPressed() || cursorComponent.CursorIsLocked);

            if (justStoppedTemporalLock)
                cursorComponent.CancelCursorLock = false;

            void UnlockCursor(ref CursorComponent cursorComponent)
            {
                cursorComponent.CursorIsLocked = false;
                cursor.Unlock();

                Mouse.current.WarpCursorPosition(cursorComponent.Position);

                //cursorComponent.Position = mousePos;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateCursorPosition(ref CursorComponent cursorComponent, Vector2 controllerDelta, Vector2 mousePos)
        {
            if (cursorComponent.AllowCameraMovement)
            {
                Mouse.current.WarpCursorPosition(cursorComponent.Position);
                return;
            }

            if (cursorComponent.CursorIsLocked)
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
