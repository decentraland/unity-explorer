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
        private const int MICROPHONE_LENGTH_SECONDS = 1;
        private const int MAX_SAMPLE_RATE = 48000; // Cap sample rate for voice chat bandwidth efficiency

        public event Action EnabledMicrophone;
        public event Action DisabledMicrophone;
        public event Action MicrophoneReady;

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
            
            InitializeMicrophone();
        }

        public void Dispose()
        {
            dclInput.VoiceChat.Talk.performed -= OnPressed;
            dclInput.VoiceChat.Talk.canceled -= OnReleased;
            voiceChatSettings.MicrophoneChanged -= OnMicrophoneChanged;
            voiceChatCallStatusService.StatusChanged -= OnCallStatusChanged;

            EnabledMicrophone = null;
            DisabledMicrophone = null;
            MicrophoneReady = null;

            if (isMicrophoneInitialized)
            {
                if (audioSource != null)
                {
                    audioSource.mute = true;
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

            if (voiceChatSettings.SelectedMicrophoneIndex >= Microphone.devices.Length)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Selected microphone index {voiceChatSettings.SelectedMicrophoneIndex} is out of range on macOS. Using default microphone.");
                MicrophoneName = Microphone.devices[0];
            }
            else
                MicrophoneName = Microphone.devices[voiceChatSettings.SelectedMicrophoneIndex];

            // Get device capabilities to determine appropriate sample rate
            int minFreq, maxFreq;
            Microphone.GetDeviceCaps(MicrophoneName, out minFreq, out maxFreq);
            
            // Use device's preferred sample rate, but cap at 48kHz for voice chat efficiency
            // If device reports specific range, use the minimum of (maxFreq, 48kHz)
            // If device supports any frequency (0,0), default to 48kHz
            int sampleRate;
            if (minFreq == 0 && maxFreq == 0)
            {
                sampleRate = MAX_SAMPLE_RATE; // Device supports any rate, use our preferred max
            }
            else
            {
                sampleRate = Mathf.Min(maxFreq, MAX_SAMPLE_RATE); // Cap at 48kHz
                // Ensure we don't go below the device minimum
                sampleRate = Mathf.Max(sampleRate, minFreq);
            }
            
            ReportHub.Log(ReportCategory.VOICE_CHAT, 
                $"Microphone device '{MicrophoneName}' capabilities - MinFreq: {minFreq}Hz, MaxFreq: {maxFreq}Hz, " +
                $"Selected SampleRate: {sampleRate}Hz");
            
            // On macOS, be more conservative with microphone settings
            try
            {
                microphoneAudioClip = Microphone.Start(MicrophoneName, MICROPHONE_LOOP, MICROPHONE_LENGTH_SECONDS, sampleRate);
                if (microphoneAudioClip == null)
                {
                    ReportHub.LogError(ReportCategory.VOICE_CHAT, "Failed to start microphone on macOS. This may indicate permission issues or device conflicts.");
                    return;
                }
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"Microphone started on macOS with sample rate: {sampleRate}Hz (device caps: {minFreq}-{maxFreq}Hz, capped at {MAX_SAMPLE_RATE}Hz)");
            }
            catch (System.Exception ex)
            {
                ReportHub.LogError(ReportCategory.VOICE_CHAT, $"Microphone initialization failed on macOS: {ex.Message}");
                return;
            }
#else
            MicrophoneName = Microphone.devices[voiceChatSettings.SelectedMicrophoneIndex];
            
            // Get device capabilities to determine appropriate sample rate
            int minFreq, maxFreq;
            Microphone.GetDeviceCaps(MicrophoneName, out minFreq, out maxFreq);
            
            // Use device's preferred sample rate, but cap at 48kHz for voice chat efficiency
            // If device reports specific range, use the minimum of (maxFreq, 48kHz)
            // If device supports any frequency (0,0), default to 48kHz
            int sampleRate;
            if (minFreq == 0 && maxFreq == 0)
            {
                sampleRate = MAX_SAMPLE_RATE; // Device supports any rate, use our preferred max
            }
            else
            {
                sampleRate = Mathf.Min(maxFreq, MAX_SAMPLE_RATE); // Cap at 48kHz
                // Ensure we don't go below the device minimum
                sampleRate = Mathf.Max(sampleRate, minFreq);
            }
            
            ReportHub.Log(ReportCategory.VOICE_CHAT, 
                $"Microphone device '{MicrophoneName}' capabilities - MinFreq: {minFreq}Hz, MaxFreq: {maxFreq}Hz, " +
                $"Selected SampleRate: {sampleRate}Hz");
            
            microphoneAudioClip = Microphone.Start(MicrophoneName, MICROPHONE_LOOP, MICROPHONE_LENGTH_SECONDS, sampleRate);
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"Microphone started with sample rate: {sampleRate}Hz (device caps: {minFreq}-{maxFreq}Hz, capped at {MAX_SAMPLE_RATE}Hz)");
            
            // Verify the actual recording sample rate matches what we requested
            if (microphoneAudioClip != null)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, 
                    $"Microphone AudioClip created - Actual Frequency: {microphoneAudioClip.frequency}Hz, " +
                    $"Channels: {microphoneAudioClip.channels}, Length: {microphoneAudioClip.length}s");
            }
#endif

            audioSource.clip = microphoneAudioClip;
            audioSource.loop = true;
            audioSource.volume = 1f;  // Keep volume at 1 so OnAudioFilterRead gets called
            
            // Force mono audio for voice chat - this ensures we always get mono input to our filter
            // This eliminates the need for channel mixing in the audio filter
            audioSource.spatialBlend = 0f;  // 2D audio (not spatial)
            audioSource.panStereo = 0f;     // Center pan (no stereo separation)
            
            audioSource.Play();

            audioFilter.enabled = false;

            EnabledMicrophone?.Invoke();
            isMicrophoneInitialized = true;
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Microphone initialized with forced mono configuration");
            
            // Update the audio filter's cached sample rate to match the microphone
            if (microphoneAudioClip != null)
            {
                audioFilter.UpdateSampleRate(microphoneAudioClip.frequency);
            }
            
            MicrophoneReady?.Invoke();
        }

        private void EnableMicrophone()
        {
            audioSource.mute = false;  // Allow audio processing - mute state controls local playback
            audioFilter.enabled = true;
            EnabledMicrophone?.Invoke();
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Enable microphone (capture and processing enabled)");
        }

        private void DisableMicrophone()
        {
            audioSource.mute = true;
            audioFilter.enabled = false;
            DisabledMicrophone?.Invoke();
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Disable microphone");
        }

        private void OnMicrophoneChanged(int newMicrophoneIndex)
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                OnMicrophoneChangedAsync(newMicrophoneIndex).Forget();
                return;
            }

            HandleMicrophoneChange(newMicrophoneIndex);
        }

        private async UniTaskVoid OnMicrophoneChangedAsync(int newMicrophoneIndex)
        {
            await using ExecuteOnMainThreadScope scope = await ExecuteOnMainThreadScope.NewScopeAsync();
            HandleMicrophoneChange(newMicrophoneIndex);
        }

        private void HandleMicrophoneChange(int newMicrophoneIndex)
        {
            bool wasTalking = IsTalking;

            if (isMicrophoneInitialized)
            {
                audioSource.Stop();
                audioSource.clip = null;
                Microphone.End(MicrophoneName);
                isMicrophoneInitialized = false;
            }

            audioFilter.ResetProcessor();

            // Always reinitialize microphone so it's ready when needed
            InitializeMicrophone();

            // Restore previous talking state if in call
            if (isInCall && wasTalking)
            {
                audioSource.mute = false;
                audioFilter.enabled = true;
            }

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"Microphone restarted with new device: {Microphone.devices[newMicrophoneIndex]}");
        }
    }
}
