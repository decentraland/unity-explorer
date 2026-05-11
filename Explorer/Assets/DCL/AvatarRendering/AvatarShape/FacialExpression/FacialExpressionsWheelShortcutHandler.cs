using System;
using UnityEngine;
using Utility;
using InputAction = UnityEngine.InputSystem.InputAction;

namespace DCL.AvatarRendering.AvatarShape.FacialExpression
{
    /// <summary>
    ///     Owns the Y key (facial expressions shortcut) release handling. Receives expression-played
    ///     notifications via <see cref="NotifyExpressionPlayed"/>; when Y is released, applies
    ///     ignore-next-release and time-based lock, then publishes
    ///     <see cref="RequestToggleFacialExpressionsWheelEvent"/>.
    /// </summary>
    public class FacialExpressionsWheelShortcutHandler : IDisposable
    {
        private const float QUICK_APPLY_LOCK_TIME = 0.5f;

        private readonly IEventBus eventBus;
        private readonly DCLInput dclInput;

        private bool ignoreNextRelease;
        private float lockUntilTime;

        public FacialExpressionsWheelShortcutHandler(IEventBus eventBus)
        {
            this.eventBus = eventBus;
            dclInput = DCLInput.Instance;
            dclInput.Shortcuts.FaceExpression.canceled += OnShortcutReleased;
        }

        public void Dispose() =>
            dclInput.Shortcuts.FaceExpression.canceled -= OnShortcutReleased;

        public virtual void NotifyExpressionPlayed(FacialExpressionTriggerSource source)
        {
            switch (source)
            {
                case FacialExpressionTriggerSource.SHORTCUT:
                    ignoreNextRelease = true;
                    break;
                case FacialExpressionTriggerSource.WHEEL_SLOT:
                    lockUntilTime = Time.time + QUICK_APPLY_LOCK_TIME;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(source), source, null);
            }
        }

        private void OnShortcutReleased(InputAction.CallbackContext _)
        {
            if (ignoreNextRelease)
            {
                ignoreNextRelease = false;
                return;
            }

            if (Time.time < lockUntilTime)
            {
                lockUntilTime = 0f;
                return;
            }

            eventBus.Publish(new RequestToggleFacialExpressionsWheelEvent());
        }
    }
}