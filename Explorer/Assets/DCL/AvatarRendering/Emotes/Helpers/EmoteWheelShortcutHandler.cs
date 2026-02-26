using System;
using UnityEngine;
using Utility;
using InputAction = UnityEngine.InputSystem.InputAction;

namespace DCL.AvatarRendering.Emotes
{
    /// <summary>
    ///     Owns the B key (EmoteWheel shortcut) release handling. Receives emote-played notifications via
    ///     <see cref="NotifyEmotePlayed" />; when B is released, applies ignore-next-release
    ///     and time-based lock, then publishes <see cref="RequestToggleEmoteWheelEvent" />.
    /// </summary>
    public class EmoteWheelShortcutHandler : IDisposable
    {
        private const float QUICK_EMOTE_LOCK_TIME = 0.5f;

        private readonly IEventBus eventBus;
        private readonly DCLInput dclInput;

        private bool ignoreNextRelease;
        private float lockUntilTime;

        public EmoteWheelShortcutHandler(IEventBus eventBus)
        {
            this.eventBus = eventBus;
            dclInput = DCLInput.Instance;
            dclInput.Shortcuts.EmoteWheel.canceled += OnEmoteWheelShortcutKeyReleased;
        }

        public void Dispose() =>
            dclInput.Shortcuts.EmoteWheel.canceled -= OnEmoteWheelShortcutKeyReleased;

        public virtual void NotifyEmotePlayed(EmoteTriggerSource source)
        {
            switch (source)
            {
                case EmoteTriggerSource.SHORTCUT:
                    ignoreNextRelease = true;
                    break;
                case EmoteTriggerSource.WHEEL_SLOT:
                    lockUntilTime = Time.time + QUICK_EMOTE_LOCK_TIME;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(source), source, null);
            }
        }

        private void OnEmoteWheelShortcutKeyReleased(InputAction.CallbackContext _)
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

            eventBus.Publish(new RequestToggleEmoteWheelEvent());
        }
    }
}
