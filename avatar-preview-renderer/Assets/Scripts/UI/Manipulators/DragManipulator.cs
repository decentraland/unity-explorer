using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Manipulators
{
    public class DragManipulator : PointerManipulator
    {
        private readonly Action<Vector2, float> _dragged;

        private bool active;
        private Vector2 _lastDelta;

        public DragManipulator(Action<Vector2, float> dragged)
        {
            _dragged = dragged;

            activators.Add(new ManipulatorActivationFilter
            {
                button = MouseButton.LeftMouse
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

            target.schedule.Execute(OnUpdate).Until(() => !active);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!active)
                return;

            _lastDelta = evt.deltaPosition;
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!active || !CanStopManipulation(evt))
                return;

            active = false;
            target.ReleasePointer(evt.pointerId);

            evt.StopPropagation();
        }

        private void OnUpdate(TimerState ts)
        {
            _dragged(_lastDelta, ts.deltaTime / 1000f);
            _lastDelta = Vector2.zero;
        }
    }
}