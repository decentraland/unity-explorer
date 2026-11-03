using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     Stands in for Clickable where a left press may also drag: released within the threshold it clicks, moved past
    ///     it the press drags until the release or the loss of the pointer and never clicks. Positions are in panel space.
    /// </summary>
    public class ClickOrDragManipulator : PointerManipulator
    {
        // Pointer travel before a press turns into a drag
        internal const float DRAG_THRESHOLD = 8f;

        // Bit of PointerEventBase.pressedButtons for the left mouse button
        internal const int LEFT_BUTTON_MASK = 1;

        private const float DRAG_THRESHOLD_SQR = DRAG_THRESHOLD * DRAG_THRESHOLD;

        public Action? Clicked;
        public Action<Vector2>? DragStarted;

        /// <summary>Panel position and travel since the previous report.</summary>
        public Action<Vector2, Vector2>? Dragged;
        public Action<Vector2>? DragEnded;

        private int pressedPointerId = PointerId.invalidPointerId;
        private Vector2 pressPosition;
        private Vector2 lastPosition;
        private bool dragging;

        /// <summary>Forgets a tracked press; a drag ends without a click.</summary>
        public void Cancel() =>
            EndPress(lastPosition, click: false);

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            target.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            target.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;

            // A press still tracked is stale: its release went where the target could not hear it
            EndPress(lastPosition, click: false);

            pressedPointerId = evt.pointerId;
            pressPosition = evt.position;
            lastPosition = evt.position;
            target.CapturePointer(evt.pointerId);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != pressedPointerId) return;

            // The button is up: the release went to an element outside the target's hearing
            if ((evt.pressedButtons & LEFT_BUTTON_MASK) == 0)
            {
                EndPress(evt.position, click: false);
                return;
            }

            Vector2 position = evt.position;

            if (!dragging)
            {
                if ((position - pressPosition).sqrMagnitude < DRAG_THRESHOLD_SQR) return;

                dragging = true;
                DragStarted?.Invoke(position);
            }

            // The event's own delta is not filled in by every source
            Dragged?.Invoke(position, position - lastPosition);
            lastPosition = position;
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != pressedPointerId) return;

            EndPress(evt.position, click: !dragging);
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId != pressedPointerId) return;

            EndPress(lastPosition, click: false);
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.pointerId != pressedPointerId) return;

            EndPress(lastPosition, click: false);
        }

        private void EndPress(Vector2 position, bool click)
        {
            if (pressedPointerId == PointerId.invalidPointerId) return;

            // Reset before the release: it raises a capture-out this press must no longer answer
            int pointerId = pressedPointerId;
            pressedPointerId = PointerId.invalidPointerId;
            bool dragged = dragging;
            dragging = false;

            if (target.HasPointerCapture(pointerId))
                target.ReleasePointer(pointerId);

            if (dragged)
                DragEnded?.Invoke(position);
            else if (click)
                Clicked?.Invoke();
        }
    }
}
