using DCL.Diagnostics;
using DCL.Settings.Settings;
using System;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using DCL.Utilities;
using Utility.Ownership;
using LiveKit.Audio;
using LiveKit.Runtime.Scripts.Audio;
using RichTypes;
using UnityEngine;

namespace DCL.VoiceChat
{
    public class VoiceChatMicrophoneHandler : IDisposable
    {
        private readonly VoiceChatSettingsAsset voiceChatSettings;
        private readonly VoiceChatConfiguration voiceChatConfiguration;

        private readonly ReactiveProperty<bool> isMicrophoneEnabledProperty;
        private float buttonPressStartTime;
        private bool hasIntentionToDisable;

        private Weak<MicrophoneRtcAudioSource> microphoneSource = Weak<MicrophoneRtcAudioSource>.Null;

        public MicrophoneSelection? CurrentMicrophoneName => voiceChatSettings.SelectedMicrophone;

        public IReadonlyReactiveProperty<bool> IsMicrophoneEnabled => isMicrophoneEnabledProperty;

        public VoiceChatMicrophoneHandler(
            VoiceChatSettingsAsset voiceChatSettings,
            VoiceChatConfiguration voiceChatConfiguration
        )
        {
            this.voiceChatSettings = voiceChatSettings;
            this.voiceChatConfiguration = voiceChatConfiguration;
            isMicrophoneEnabledProperty = new ReactiveProperty<bool>(false);

            DCLInput.Instance.VoiceChat.Talk!.performed += OnPressed;
            DCLInput.Instance.VoiceChat.Talk.canceled += OnReleased;
            voiceChatSettings.MicrophoneChanged += OnMicrophoneChanged;
        }

        public void Dispose()
        {
            isMicrophoneEnabledProperty.Dispose();
            DCLInput.Instance.VoiceChat.Talk!.performed -= OnPressed;
            DCLInput.Instance.VoiceChat.Talk.canceled -= OnReleased;
            voiceChatSettings.MicrophoneChanged -= OnMicrophoneChanged;
        }

        /// <summary>
        /// Don't keep reference.
        /// Must be consumed in place without async operation.
        /// Source is not guaranteed to be alive after the scope.
        /// </summary>
        private bool TryGetCurrentMicrophoneSourceIfInCall(out MicrophoneRtcAudioSource? microphoneRtcAudioSource)
        {
            Option<MicrophoneRtcAudioSource> resource = microphoneSource.Resource;
            microphoneRtcAudioSource = resource.Has ? resource.Value : null;
            return resource.Has;
        }

        private void OnPressed(InputAction.CallbackContext obj)
        {
            if (TryGetCurrentMicrophoneSourceIfInCall(out var source))
            {
                buttonPressStartTime = Time.time;
                hasIntentionToDisable = source!.IsRecording; // Disable microphone on release if it was recording
                EnableMicrophone();
            }
        }

        private void OnReleased(InputAction.CallbackContext obj)
        {
            if (TryGetCurrentMicrophoneSourceIfInCall(out _))
            {
                float pressDuration = Time.time - buttonPressStartTime;

                // If the button was held for longer than the threshold, treat it as push-to-talk and stop communication on release
                bool shouldDisableByThreshold = pressDuration >= voiceChatConfiguration.HoldThresholdInSeconds;

                if (shouldDisableByThreshold || hasIntentionToDisable)
                    DisableMicrophone();
            }
        }

        public void ToggleMicrophone()
        {
            var weakMicrophoneSource = microphoneSource.Resource;

            if (weakMicrophoneSource.Has)
            {
                MicrophoneRtcAudioSource source = weakMicrophoneSource.Value;
                source.Toggle();
                isMicrophoneEnabledProperty.Value = source.IsRecording;
            }
        }

        public void Assign(Weak<MicrophoneRtcAudioSource> newSource)
        {
            microphoneSource = newSource;
        }

        private void EnableMicrophone()
        {
            var weakMicrophoneSource = microphoneSource.Resource;
            if (weakMicrophoneSource.Has) weakMicrophoneSource.Value.Start();
            isMicrophoneEnabledProperty.Value = true;
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Enabled microphone");
        }

        private void DisableMicrophone()
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                DisableMicrophoneAsync().Forget();
                return;
            }

            DisableMicrophoneInternal();
        }

        private async UniTaskVoid DisableMicrophoneAsync()
        {
            await UniTask.SwitchToMainThread();
            DisableMicrophoneInternal();
        }

        private void DisableMicrophoneInternal()
        {
            var weakMicrophoneSource = microphoneSource.Resource;
            if (weakMicrophoneSource.Has) weakMicrophoneSource.Value.Stop();
            isMicrophoneEnabledProperty.Value = false;
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Disabled microphone");
        }

        private void OnMicrophoneChanged(MicrophoneSelection microphoneName)
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, "Microphone change dispatching to main thread (async)");
                OnMicrophoneChangedAsync(microphoneName).Forget();
                return;
            }

            ReportHub.Log(ReportCategory.VOICE_CHAT, "Microphone change executing on main thread (sync)");
            TryHandleMicrophoneChange(microphoneName);
        }

        private async UniTaskVoid OnMicrophoneChangedAsync(MicrophoneSelection microphoneName)
        {
            await UniTask.SwitchToMainThread();
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Microphone change executing after main thread dispatch (async)");
            TryHandleMicrophoneChange(microphoneName);
        }

        private void TryHandleMicrophoneChange(MicrophoneSelection microphoneName)
        {
            var weakMicrophoneSource = microphoneSource.Resource;

            if (weakMicrophoneSource.Has == false)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Microphone source is already disposed: {microphoneName}");
                return;
            }

            var result = weakMicrophoneSource.Value.SwitchMicrophone(microphoneName);

            if (result.Success == false)
                ReportHub.LogError(ReportCategory.VOICE_CHAT, $"Cannot select microphone: {result.ErrorMessage}");
        }

        public void EnableMicrophoneForCall()
        {
            EnableMicrophone();
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Microphone enabled for call (room connected)");
        }

        public void DisableMicrophoneForCall()
        {
            DisableMicrophone();
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Microphone disabled for call");
        }
    }
}
