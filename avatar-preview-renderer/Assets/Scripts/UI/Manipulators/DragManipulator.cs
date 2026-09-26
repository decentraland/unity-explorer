using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Manipulators
{
    public class DragManipulator : PointerManipulator
    {
        private readonly Action<Vector2, float> _dragged;
        private readonly bool _accumulateDelta;
        private readonly Action<bool> _activeChanged;

        private bool active;
        private Vector2 _lastDelta;

        /// <param name="accumulateDelta">
        /// Sum every pointer move between scheduler ticks instead of keeping only the newest. Needed to
        /// track the cursor 1:1; rotation is tuned around the lossy default.
        /// </param>
        /// <param name="activeChanged">
        /// Raised true when a drag starts and false when it ends, for callers reflecting the drag
        /// elsewhere - a cursor, say. Fires off the same activation filter the drag itself uses.
        /// </param>
        public DragManipulator(Action<Vector2, float> dragged, MouseButton activatorButton = MouseButton.LeftMouse,
            bool accumulateDelta = false, Action<bool> activeChanged = null)
        {
            _dragged = dragged;
            _accumulateDelta = accumulateDelta;
            _activeChanged = activeChanged;

            activators.Add(new ManipulatorActivationFilter
            {
                button = activatorButton
            });
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (!CanStartManipulation(evt))
                return;

            active = true;
            target.CapturePointer(evt.pointerId);
            _activeChanged?.Invoke(true);

            target.schedule.Execute(OnUpdate).Until(() => !active);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!active)
                return;

            // Pointer moves outpace the scheduler draining this - a 1000Hz mouse fires ~16 per frame at
            // 60fps - so overwriting would throw most of the movement away.
            var delta = (Vector2)evt.deltaPosition;
            _lastDelta = _accumulateDelta ? _lastDelta + delta : delta;
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!active || !CanStopManipulation(evt))
                return;

            active = false;
            target.ReleasePointer(evt.pointerId);
            _activeChanged?.Invoke(false);

            evt.StopPropagation();
        }

        private void OnUpdate(TimerState ts)
        {
            _dragged(_lastDelta, ts.deltaTime / 1000f);
            _lastDelta = Vector2.zero;
        }
    }
}