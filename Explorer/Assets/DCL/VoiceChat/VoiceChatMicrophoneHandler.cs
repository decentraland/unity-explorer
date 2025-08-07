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

        private Weak<MicrophoneRtcAudioSource> source = Weak<MicrophoneRtcAudioSource>.Null;
        private bool isInCall;

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
            isInCall = false;
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
            if (!isInCall) return;

            // Start the microphone immediately when button is pressed
            // If it's a quick press, we'll handle it in OnReleased
            var option = source.Resource;
            if (option.Has) option.Value.Start();
        }

        private void OnReleased(InputAction.CallbackContext obj)
        {
            if (!isInCall) return;
            DisableMicrophone();
        }

        public void ToggleMicrophone()
        {
            if (!isInCall) return;

            var option = source.Resource;
            if (option.Has) option.Value.Toggle();
        }

        public void Assign(Weak<MicrophoneRtcAudioSource> newSource)
        {
            source = newSource;
        }

        // TODO
        // #if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        //
        //         // On macOS, check if we have microphone permission
        //         if (Microphone.devices.Length == 0)
        //         {
        //             ReportHub.LogWarning(ReportCategory.VOICE_CHAT, "No microphone devices found on macOS. This may indicate missing microphone permissions.");
        //             return;
        //         }
        // #endif

        private void EnableMicrophone()
        {
            var option = source.Resource;
            if (option.Has) option.Value.Start();

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
            var option = source.Resource;
            if (option.Has) option.Value.Stop();

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
            HandleMicrophoneChange(microphoneName);
        }

        private async UniTaskVoid OnMicrophoneChangedAsync(MicrophoneSelection microphoneName)
        {
            await UniTask.SwitchToMainThread();
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Microphone change executing after main thread dispatch (async)");
            HandleMicrophoneChange(microphoneName);
        }

        private void HandleMicrophoneChange(MicrophoneSelection microphoneName)
        {
            var option = source.Resource;

            if (option.Has == false)
            {
                ReportHub.LogError(ReportCategory.VOICE_CHAT, $"Microphone source is already disposed: {microphoneName}");
                return;
            }

            var result = option.Value.SwitchMicrophone(microphoneName);

            if (result.Success == false) { ReportHub.LogError(ReportCategory.VOICE_CHAT, $"Cannot select microphone: {result.ErrorMessage}"); }
        }

        public void EnableMicrophoneForCall()
        {
            isInCall = true;
            EnableMicrophone();
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Microphone enabled for call (room connected)");
        }

        public void DisableMicrophoneForCall()
        {
            isInCall = false;
            DisableMicrophone();
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Microphone disabled for call");
        }
    }
}
