using DCL.Diagnostics;
using DCL.Settings.Settings;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Utility.Multithreading;
using Cysharp.Threading.Tasks;

namespace DCL.VoiceChat
{
    public class VoiceChatMicrophoneHandler : IDisposable
    {
        private const bool MICROPHONE_LOOP = true;
        private const int MICROPHONE_SAMPLE_RATE = 48000;
        private const int MICROPHONE_LENGTH_SECONDS = 1;

        public event Action EnabledMicrophone;
        public event Action DisabledMicrophone;

        private readonly DCLInput dclInput;
        private readonly VoiceChatSettingsAsset voiceChatSettings;
        private readonly VoiceChatConfiguration voiceChatConfiguration;
        private readonly AudioSource audioSource;
        private readonly VoiceChatMicrophoneAudioFilter audioFilter;
        private readonly IVoiceChatCallStatusService voiceChatCallStatusService;

        private AudioClip microphoneAudioClip;

        private bool isMicrophoneInitialized;
        private bool isInCall;

        private float buttonPressStartTime;

        public bool IsTalking { get; private set; }
        public string MicrophoneName { get; private set; }
        public int MicrophoneSampleRate { get; private set; }

        public VoiceChatMicrophoneHandler(
            DCLInput dclInput,
            VoiceChatSettingsAsset voiceChatSettings,
            VoiceChatConfiguration voiceChatConfiguration,
            AudioSource audioSource,
            VoiceChatMicrophoneAudioFilter audioFilter,
            IVoiceChatCallStatusService voiceChatCallStatusService)
        {
            this.dclInput = dclInput;
            this.voiceChatSettings = voiceChatSettings;
            this.voiceChatConfiguration = voiceChatConfiguration;
            this.audioSource = audioSource;
            this.audioFilter = audioFilter;
            this.voiceChatCallStatusService = voiceChatCallStatusService;

            dclInput.VoiceChat.Talk.performed += OnPressed;
            dclInput.VoiceChat.Talk.canceled += OnReleased;
            voiceChatSettings.MicrophoneChanged += OnMicrophoneChanged;
            voiceChatCallStatusService.StatusChanged += OnCallStatusChanged;

            isInCall = false;
        }

        public void Dispose()
        {
            dclInput.VoiceChat.Talk.performed -= OnPressed;
            dclInput.VoiceChat.Talk.canceled -= OnReleased;
            voiceChatSettings.MicrophoneChanged -= OnMicrophoneChanged;
            voiceChatCallStatusService.StatusChanged -= OnCallStatusChanged;

            if (isMicrophoneInitialized)
            {
                if (audioSource != null)
                {
                    audioSource.volume = 0f;
                    StopAudioSource();
                    audioSource.clip = null;
                }
                Microphone.End(MicrophoneName);

                if (audioFilter != null)
                    audioFilter.enabled = false;
            }
        }

        private void StopAudioSource()
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                StopAudioSourceAsync().Forget();
                return;
            }

            if (audioSource != null)
                audioSource.Stop();
        }

        private async UniTaskVoid StopAudioSourceAsync()
        {
            await using ExecuteOnMainThreadScope scope = await ExecuteOnMainThreadScope.NewScopeAsync();
            if (audioSource != null)
                audioSource.Stop();
        }

        private void OnCallStatusChanged(VoiceChatStatus newStatus)
        {
            switch (newStatus)
            {
                case VoiceChatStatus.VOICE_CHAT_ENDING_CALL:
                case VoiceChatStatus.VOICE_CHAT_ENDED_CALL:
                case VoiceChatStatus.DISCONNECTED:
                    isInCall = false;
                    DisableMicrophone();
                    break;
                case VoiceChatStatus.VOICE_CHAT_IN_CALL:
                case VoiceChatStatus.VOICE_CHAT_STARTED_CALL:
                case VoiceChatStatus.VOICE_CHAT_STARTING_CALL:
                    isInCall = true;
                    break;
                case VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL: break;
                default: throw new ArgumentOutOfRangeException(nameof(newStatus), newStatus, null);
            }
        }


        private void OnPressed(InputAction.CallbackContext obj)
        {
            if (!isInCall) return;

            buttonPressStartTime = Time.time;

            // Start the microphone immediately when button is pressed
            // If it's a quick press, we'll handle it in OnReleased
            if (!IsTalking)
                EnableMicrophone();
        }

        private void OnReleased(InputAction.CallbackContext obj)
        {
            if (!isInCall) return;

            float pressDuration = Time.time - buttonPressStartTime;

            // If the button was held for longer than the threshold, treat it as push-to-talk and stop communication on release
            if (pressDuration >= voiceChatConfiguration.HoldThresholdInSeconds)
            {
                IsTalking = false;
                DisableMicrophone();
            }
            else
            {
                if (IsTalking)
                    DisableMicrophone();

                IsTalking = !IsTalking;
            }
        }

        public void ToggleMicrophone()
        {
            if (!isInCall) return;

            if(IsTalking)
                EnableMicrophone();
            else
                DisableMicrophone();
        }

        private void InitializeMicrophone()
        {
            if (isMicrophoneInitialized)
                return;

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            // On macOS, check if we have microphone permission
            if (Microphone.devices.Length == 0)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, "No microphone devices found on macOS. This may indicate missing microphone permissions.");
                return;
            }
#endif

            if (voiceChatSettings.SelectedMicrophoneIndex >= Microphone.devices.Length)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Selected microphone index {voiceChatSettings.SelectedMicrophoneIndex} is out of range on macOS. Using default microphone.");
                MicrophoneName = Microphone.devices[0];
            }
            else
                MicrophoneName = Microphone.devices[voiceChatSettings.SelectedMicrophoneIndex];



            MicrophoneSampleRate = VoiceChatMicrophoneHelper.GetOptimalMicrophoneSampleRate(MicrophoneName);

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            // On macOS, be more conservative with microphone settings
            try
            {
                microphoneAudioClip = Microphone.Start(MicrophoneName, MICROPHONE_LOOP, MICROPHONE_LENGTH_SECONDS, MicrophoneSampleRate);
                if (microphoneAudioClip == null)
                {
                    ReportHub.LogError(ReportCategory.VOICE_CHAT, "Failed to start microphone on macOS. This may indicate permission issues or device conflicts.");
                    return;
                }
            }
            catch (System.Exception ex)
            {
                ReportHub.LogError(ReportCategory.VOICE_CHAT, $"Microphone initialization failed on macOS: {ex.Message}");
                return;
            }
#else
            microphoneAudioClip = Microphone.Start(MicrophoneName, MICROPHONE_LOOP, MICROPHONE_LENGTH_SECONDS, MicrophoneSampleRate);
#endif

            audioSource.clip = microphoneAudioClip;
            audioSource.volume = 0f;

            isMicrophoneInitialized = true;
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"Microphone initialized with sample rate: {MicrophoneSampleRate}Hz");
        }



        private void EnableMicrophone()
        {
            if (!isMicrophoneInitialized)
                InitializeMicrophone();

            audioSource.loop = true;
            audioSource.Play();
            audioSource.volume = 1f;
            audioFilter.SetFilterActive(true);
            EnabledMicrophone?.Invoke();
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Enabled microphone");
        }

        private void DisableMicrophone()
        {
            audioSource.loop = false;
            StopAudioSource();
            audioSource.volume = 0f;
            audioFilter.SetFilterActive(false);
            DisabledMicrophone?.Invoke();
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Disabled microphone");
        }

        private void OnMicrophoneChanged(int newMicrophoneIndex)
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, "Microphone change dispatching to main thread (async)");
                OnMicrophoneChangedAsync(newMicrophoneIndex).Forget();
                return;
            }

            ReportHub.Log(ReportCategory.VOICE_CHAT, "Microphone change executing on main thread (sync)");
            HandleMicrophoneChange(newMicrophoneIndex);
        }

        private async UniTaskVoid OnMicrophoneChangedAsync(int newMicrophoneIndex)
        {
            await using ExecuteOnMainThreadScope scope = await ExecuteOnMainThreadScope.NewScopeAsync();
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Microphone change executing after main thread dispatch (async)");
            HandleMicrophoneChange(newMicrophoneIndex);
        }

        private void HandleMicrophoneChange(int newMicrophoneIndex)
        {
            bool wasTalking = IsTalking;

            if (isMicrophoneInitialized)
            {
                DisableMicrophone();
                audioSource.clip = null;
                Microphone.End(MicrophoneName);
                isMicrophoneInitialized = false;
            }

            if (isInCall)
            {
                InitializeMicrophone();

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"Microphone Initialized with new device: {Microphone.devices[newMicrophoneIndex]}");

                if (wasTalking)
                {
                    EnableMicrophone();
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"Microphone Enabled with new device: {Microphone.devices[newMicrophoneIndex]}");
                }
            }

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"Microphone restarted with new device: {Microphone.devices[newMicrophoneIndex]}");
        }
    }
}

