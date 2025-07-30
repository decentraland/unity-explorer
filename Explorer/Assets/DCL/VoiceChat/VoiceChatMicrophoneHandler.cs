using DCL.Diagnostics;
using DCL.Settings.Settings;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using LiveKit.Audio;
using RichTypes;
using System.Threading;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.VoiceChat
{
    public class VoiceChatMicrophoneHandler : IDisposable
    {
        private const bool MICROPHONE_LOOP = true;
        private const int MICROPHONE_LENGTH_SECONDS = 1;

        private readonly VoiceChatSettingsAsset voiceChatSettings;
        private readonly VoiceChatConfiguration voiceChatConfiguration;
        private readonly VoiceChatMicrophoneAudioFilter audioFilter;
        private readonly AudioSource audioSource;

        private AudioClip microphoneAudioClip;
        private bool isMicrophoneInitialized;
        private bool isInCall;
        private bool isTalking { get; set; }
        private CancellationTokenSource microphoneChangeCts;
        private int microphoneSampleRate;
        private string microphoneName;
        private float buttonPressStartTime;
        private MicrophoneRtcAudioSource2 rtcAudioSource;

        public VoiceChatMicrophoneAudioFilter AudioFilter => audioFilter;
        public AudioSource AudioSource => audioSource;

        public event Action EnabledMicrophone;
        public event Action DisabledMicrophone;

        public VoiceChatMicrophoneHandler(
            VoiceChatSettingsAsset voiceChatSettings,
            VoiceChatConfiguration voiceChatConfiguration,
            AudioSource audioSource,
            VoiceChatMicrophoneAudioFilter audioFilter)
        {
            this.voiceChatSettings = voiceChatSettings;
            this.voiceChatConfiguration = voiceChatConfiguration;
            this.audioSource = audioSource;
            this.audioFilter = audioFilter;

            DCLInput.Instance.VoiceChat.Talk.performed += OnPressed;
            DCLInput.Instance.VoiceChat.Talk.canceled += OnReleased;
            voiceChatSettings.MicrophoneChanged += OnMicrophoneChanged;
            isInCall = false;
        }

        public void Dispose()
        {
            DCLInput.Instance.VoiceChat.Talk.performed -= OnPressed;
            DCLInput.Instance.VoiceChat.Talk.canceled -= OnReleased;
            voiceChatSettings.MicrophoneChanged -= OnMicrophoneChanged;

            microphoneChangeCts?.SafeCancelAndDispose();

            if (isMicrophoneInitialized)
            {
                if (audioSource != null)
                {
                    audioSource.volume = 0f;
                    audioSource.Stop();
                    audioSource.clip = null;
                }

                Microphone.End(microphoneName);

                if (audioFilter != null)
                    audioFilter.enabled = false;
            }
        }

        private void OnPressed(InputAction.CallbackContext obj)
        {
            if (!isInCall) return;

            buttonPressStartTime = Time.time;

            // Start the microphone immediately when button is pressed
            // If it's a quick press, we'll handle it in OnReleased
            if (!isTalking)
                EnableMicrophone();
        }

        private void OnReleased(InputAction.CallbackContext obj)
        {
            if (!isInCall) return;

            float pressDuration = Time.time - buttonPressStartTime;

            // If the button was held for longer than the threshold, treat it as push-to-talk and stop communication on release
            if (pressDuration >= voiceChatConfiguration.HoldThresholdInSeconds)
            {
                isTalking = false;
                DisableMicrophone();
            }
            else
            {
                if (isTalking)
                    DisableMicrophone();

                isTalking = !isTalking;
            }
        }

        public void ToggleMicrophone()
        {
            if (!isInCall)
                return;

            if (!isTalking)
                EnableMicrophone();
            else
                DisableMicrophone();

            isTalking = !isTalking;
        }

        public void Reset()
        {
            if (audioFilter != null)
            {
                audioFilter.Reset();
                audioFilter.SetFilterActive(true);
            }

            isTalking = false;

            ReportHub.Log(ReportCategory.VOICE_CHAT, "Microphone handler reset for new call");
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
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Selected microphone index {voiceChatSettings.SelectedMicrophoneIndex} is out of range. Using default microphone.");
                microphoneName = Microphone.devices[0];
            }
            else
                microphoneName = Microphone.devices[voiceChatSettings.SelectedMicrophoneIndex];

            microphoneSampleRate = VoiceChatMicrophoneHelper.GetOptimalMicrophoneSampleRate(microphoneName);

            microphoneAudioClip = Microphone.Start(microphoneName, MICROPHONE_LOOP, MICROPHONE_LENGTH_SECONDS, 48000); //microphoneSampleRate); REMOVED TO COMPLY WITH EXAMPLE

            audioSource.clip = microphoneAudioClip;


            AudioSource.volume = 0f;

            isMicrophoneInitialized = true;
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"Microphone initialized with sample rate: {microphoneSampleRate}Hz");

            // If we're in a call, wait for fresh audio data before proceeding
            if (isInCall)
            {
                WaitAndReinitializeMicrophoneAsync(false, voiceChatSettings.SelectedMicrophoneIndex, microphoneChangeCts.Token).Forget();
            }
        }

        private void EnableMicrophone()
        {
            if (!isMicrophoneInitialized)
                InitializeMicrophone();

            audioSource.loop = true;
            audioSource.Play();
            audioSource.volume = 1f;
            audioFilter.enabled = true;
            audioFilter.SetFilterActive(true);
            EnabledMicrophone?.Invoke();
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
            if (audioSource != null)
            {
                audioSource.loop = false;
                audioSource.Stop();
                audioSource.volume = 0f;
            }
            if (audioFilter != null)
            {
                audioFilter.enabled = false;
                audioFilter.SetFilterActive(false);
            }
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
            await UniTask.SwitchToMainThread();
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Microphone change executing after main thread dispatch (async)");
            HandleMicrophoneChange(newMicrophoneIndex);
        }

        private void HandleMicrophoneChange(int newMicrophoneIndex)
        {
            microphoneChangeCts = microphoneChangeCts.SafeRestart();

            bool wasTalking = isTalking;

            if (isMicrophoneInitialized)
            {
                DisableMicrophone();
                Microphone.End(microphoneName);

                if (microphoneAudioClip != null)
                {
                    Object.Destroy(microphoneAudioClip);
                    microphoneAudioClip = null;
                }

                audioSource.clip = null;
                isMicrophoneInitialized = false;
            }

            if (isInCall)
                WaitAndReinitializeMicrophoneAsync(wasTalking, newMicrophoneIndex, microphoneChangeCts.Token).Forget();
            else
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"Microphone change noted but not in call: {Microphone.devices[newMicrophoneIndex]}");
        }

        private async UniTaskVoid WaitAndReinitializeMicrophoneAsync(bool wasTalking, int newMicrophoneIndex, CancellationToken ct)
        {
            try
            {
                // Wait to ensure all audio system cleanup is complete and buffers are flushed //COMMENTED TO MATCH EXAMPLE
                //await UniTask.Delay(voiceChatConfiguration.MicrophoneReinitDelayMs, cancellationToken: ct); //COMMENTED TO MATCH EXAMPLE

                if (ct.IsCancellationRequested || !isInCall)
                {
                    ReportHub.Log(ReportCategory.VOICE_CHAT, "Microphone change operation cancelled or no longer in call");
                    return;
                }

                InitializeMicrophone();

                //await WaitForFreshMicrophoneDataAsync(ct); //COMMENTED TO MATCH EXAMPLE

                ReportHub.Log(ReportCategory.VOICE_CHAT, $"Microphone Initialized with new device after delay: {Microphone.devices[newMicrophoneIndex]}");

                if (wasTalking && !ct.IsCancellationRequested)
                    EnableMicrophone();
            }
            catch (OperationCanceledException)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, "Microphone change operation was cancelled");
            }
        }

        private async UniTask WaitForFreshMicrophoneDataAsync(CancellationToken ct)
        {
            if (microphoneAudioClip == null || !Microphone.IsRecording(microphoneName))
                return;

            if (audioSource != null)
            {
                audioSource.time = 0f;
                audioSource.timeSamples = 0;
            }

            // Wait for microphone to complete at least one full recording cycle
            // This ensures we have fresh audio data, not stale data from the previous device
            int initialPosition = Microphone.GetPosition(microphoneName);
            int targetSamples = microphoneSampleRate / 2;

            var waitTime = 0;
            int maxWaitTime = voiceChatConfiguration.MaxFreshDataWaitTimeMs;

            while (waitTime < maxWaitTime && !ct.IsCancellationRequested)
            {
                await UniTask.Delay(voiceChatConfiguration.FreshDataCheckDelayMs, cancellationToken: ct);
                waitTime += voiceChatConfiguration.FreshDataCheckDelayMs;

                int currentPosition = Microphone.GetPosition(microphoneName);

                // Check if we've recorded enough fresh samples or if the recording has looped
                if (currentPosition > targetSamples || currentPosition < initialPosition)
                {
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"Fresh microphone data detected after {waitTime}ms");
                    break;
                }
            }
            if (audioSource != null && !ct.IsCancellationRequested)
            {
                audioSource.time = 0f;
                audioSource.timeSamples = 0;
            }
        }

        public void EnableMicrophoneForCall()
        {
            isInCall = true;
            isTalking = true;
            EnableMicrophone();
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Microphone enabled for call (room connected)");
        }

        public void DisableMicrophoneForCall()
        {
            if (!isInCall) return;
            isInCall = false;
            isTalking = false;
            DisableMicrophone();
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Microphone disabled for call (room disconnected)");
        }
    }
}
