using DCL.Diagnostics;
using DCL.Settings.Settings;
using System;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using DCL.Utilities;
using Utility.Ownership;
using LiveKit.Audio;
using LiveKit.Runtime.Scripts.Audio;

namespace DCL.VoiceChat
{
    public class VoiceChatMicrophoneHandler : IDisposable
    {
        private readonly VoiceChatSettingsAsset voiceChatSettings;
        private readonly ReactiveProperty<bool> isMicrophoneEnabledProperty;

        private Weak<MicrophoneRtcAudioSource> microphoneSource = Weak<MicrophoneRtcAudioSource>.Null;

        public MicrophoneSelection? CurrentMicrophoneName => voiceChatSettings.SelectedMicrophone;

        public IReadonlyReactiveProperty<bool> IsMicrophoneEnabled => isMicrophoneEnabledProperty;

        public VoiceChatMicrophoneHandler(
            VoiceChatSettingsAsset voiceChatSettings)
        {
            this.voiceChatSettings = voiceChatSettings;
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

        private void OnPressed(InputAction.CallbackContext obj)
        {
            EnableMicrophone();
        }

        private void OnReleased(InputAction.CallbackContext obj)
        {
            DisableMicrophone();
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
