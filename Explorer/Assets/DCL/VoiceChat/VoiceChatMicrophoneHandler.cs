using DCL.Diagnostics;
using DCL.Settings.Settings;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Utility.Multithreading;
using Cysharp.Threading.Tasks;
using LiveKit;

namespace DCL.VoiceChat
{
    internal static class MacOSMicrophoneHelper
    {
        internal static bool TryInitializeMicrophone(VoiceChatSettingsAsset voiceChatSettings, out string microphoneName, out AudioClip microphoneAudioClip, out int actualSampleRate)
        {
            microphoneName = null;
            microphoneAudioClip = null;
            actualSampleRate = 0;

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            int microphoneSampleRate;
            // On macOS, check if we have microphone permission
            if (Microphone.devices.Length == 0)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, "No microphone devices found on macOS. This may indicate missing microphone permissions. - Stack trace: {System.Environment.StackTrace}");
                return false;
            }

            if (voiceChatSettings.SelectedMicrophoneIndex >= Microphone.devices.Length)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Selected microphone index {voiceChatSettings.SelectedMicrophoneIndex} is out of range on macOS. Using default microphone. - Stack trace: {System.Environment.StackTrace}");
                microphoneName = Microphone.devices[0];
            }
            else
                microphoneName = Microphone.devices[voiceChatSettings.SelectedMicrophoneIndex];

            // Get device capabilities to determine optimal microphone sample rate
            int minFreq, maxFreq;
            Microphone.GetDeviceCaps(microphoneName, out minFreq, out maxFreq);

            // Use device's preferred sample rate, but cap at 48kHz for voice chat efficiency
            int unitySampleRate = AudioSettings.outputSampleRate;
            if (minFreq == 0 && maxFreq == 0)
            {
                // Device supports any rate - prefer Unity's output rate, fallback to 48kHz
                microphoneSampleRate = (unitySampleRate <= VoiceChatMicrophoneHandler.MAX_SAMPLE_RATE) ? unitySampleRate : VoiceChatMicrophoneHandler.MAX_SAMPLE_RATE;
            }
            else
            {
                microphoneSampleRate = Mathf.Min(maxFreq, VoiceChatMicrophoneHandler.MAX_SAMPLE_RATE); // Cap at 48kHz
                // Ensure we don't go below the device minimum
                microphoneSampleRate = Mathf.Max(microphoneSampleRate, minFreq);
            }

            ReportHub.Log(ReportCategory.VOICE_CHAT,
                $"Microphone device '{microphoneName}' capabilities - MinFreq: {minFreq}Hz, MaxFreq: {maxFreq}Hz, " +
                $"Selected: {microphoneSampleRate}Hz (Unity output: {unitySampleRate}Hz) - Stack trace: {System.Environment.StackTrace}");

            // On macOS, be more conservative with microphone settings
            try
            {
                microphoneAudioClip = Microphone.Start(microphoneName, VoiceChatMicrophoneHandler.MICROPHONE_LOOP, VoiceChatMicrophoneHandler.MICROPHONE_LENGTH_SECONDS, microphoneSampleRate);
                if (microphoneAudioClip == null)
                {
                    ReportHub.LogError(ReportCategory.VOICE_CHAT, "Failed to start microphone on macOS. This may indicate permission issues or device conflicts. - Stack trace: {System.Environment.StackTrace}");
                    return false;
                }
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"Microphone started on macOS with sample rate: {microphoneSampleRate}Hz (device optimal) - Stack trace: {System.Environment.StackTrace}");

                // Always use the actual microphone frequency for audio filter processing
                actualSampleRate = microphoneAudioClip != null ? microphoneAudioClip.frequency : microphoneSampleRate;
                return true;
            }
            catch (System.Exception ex)
            {
                ReportHub.LogError(ReportCategory.VOICE_CHAT, $"Microphone initialization failed on macOS: {ex.Message} - Stack trace: {System.Environment.StackTrace}");
                return false;
            }
#else
            return false;
#endif
        }
    }

    public class VoiceChatMicrophoneHandler : IDisposable
    {
        internal const bool MICROPHONE_LOOP = true;
        internal const int MICROPHONE_LENGTH_SECONDS = 1; // Unity minimum - cannot be less than 1 second
        internal const int MAX_SAMPLE_RATE = 48000; // Cap sample rate for voice chat bandwidth efficiency

        private const float AUDIO_SOURCE_VOLUME = 1f;
        private const float SPATIAL_BLEND_2D = 0f; // 2D audio (not spatial)
        private const float CENTER_PAN = 0f; // Center pan (no stereo separation)

        public event Action EnabledMicrophone;
        public event Action DisabledMicrophone;
        public event Action MicrophoneReady;
        public event Action<RtcAudioSource> RtcAudioSourceReconfigured;

        private readonly DCLInput dclInput;
        private readonly VoiceChatSettingsAsset voiceChatSettings;
        private readonly VoiceChatConfiguration voiceChatConfiguration;
        private readonly AudioSource audioSource;
        private readonly VoiceChatMicrophoneAudioFilter audioFilter;
        private readonly IVoiceChatCallStatusService voiceChatCallStatusService;

        private AudioClip microphoneAudioClip;
        private RtcAudioSource rtcAudioSource;

        private bool isMicrophoneInitialized;
        private bool isInCall;
        private float buttonPressStartTime;
        private string microphoneName;

        public bool IsTalking { get; private set; }

        public string MicrophoneName
        {
            get => microphoneName;

            private set => microphoneName = value;
        }

        public RtcAudioSource RtcAudioSource => rtcAudioSource;

        public VoiceChatMicrophoneHandler
           (
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
            RtcAudioSourceReconfigured = null;

            // Stop and dispose LiveKit audio source
            if (rtcAudioSource != null)
            {
                try
                {
                    rtcAudioSource.Stop();
                    ReportHub.Log(ReportCategory.VOICE_CHAT, "RtcAudioSource stopped during dispose");
                }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Failed to stop RtcAudioSource during dispose: {ex.Message}");
                }
                
                rtcAudioSource.Dispose();
                rtcAudioSource = null;
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"RtcAudioSource disposed - Remaining subscribers: {GetAudioFilterSubscriberCount()} - Stack trace: {System.Environment.StackTrace}");
            }

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
                {
                    audioFilter.ResetProcessor();
                }
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

        private void InitializeMicrophone(bool initializeRtcAudioSource = true)
        {
            if (isMicrophoneInitialized)
                return;

            var actualConfig = AudioSettings.GetConfiguration();
            int unitySampleRate = AudioSettings.outputSampleRate;

            ReportHub.Log(ReportCategory.VOICE_CHAT,
                $"Unity audio config - Configured: {actualConfig.sampleRate}Hz, BufferSize: {actualConfig.dspBufferSize}, " +
                $"Actual Output: {unitySampleRate}Hz - Stack trace: {System.Environment.StackTrace}");

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            if (actualConfig.sampleRate != unitySampleRate)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT,
                    $"macOS Core Audio override detected - Unity configured: {actualConfig.sampleRate}Hz, " +
                    $"but Core Audio using: {unitySampleRate}Hz. Using actual rate for LiveKit compatibility. - Stack trace: {System.Environment.StackTrace}");
            }
            else
            {
            ReportHub.Log(ReportCategory.VOICE_CHAT,
                    $"macOS Core Audio matches Unity config: {unitySampleRate}Hz - Stack trace: {System.Environment.StackTrace}");
            }
#endif

            // Try macOS-specific initialization first
            bool macOSSuccess = MacOSMicrophoneHelper.TryInitializeMicrophone(voiceChatSettings, out string tempMicrophoneName, out microphoneAudioClip, out int actualSampleRate);

            if (macOSSuccess)
            {
                // Set the microphone name property for macOS success path
                MicrophoneName = tempMicrophoneName;
            }
            else
            {
                // Non-macOS initialization
                if (Microphone.devices.Length == 0)
                {
                    ReportHub.LogError(ReportCategory.VOICE_CHAT, "No microphone devices found. Cannot initialize microphone.");
                    return;
                }

                if (voiceChatSettings.SelectedMicrophoneIndex >= Microphone.devices.Length)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Selected microphone index {voiceChatSettings.SelectedMicrophoneIndex} is out of range. Using default microphone. - Stack trace: {System.Environment.StackTrace}");
                    MicrophoneName = Microphone.devices[0];
                }
                else
                    MicrophoneName = Microphone.devices[voiceChatSettings.SelectedMicrophoneIndex];

                // Get device capabilities to determine optimal microphone sample rate
                int minFreq, maxFreq;
                Microphone.GetDeviceCaps(MicrophoneName, out minFreq, out maxFreq);

                // Use device's preferred sample rate, but cap at 48kHz for voice chat efficiency
                int requestedSampleRate;
                if (minFreq == 0 && maxFreq == 0)
                {
                    // Device supports any rate - prefer Unity's output rate, fallback to 48kHz
                    requestedSampleRate = (unitySampleRate <= MAX_SAMPLE_RATE) ? unitySampleRate : MAX_SAMPLE_RATE;
                }
                else
                {
                    requestedSampleRate = Mathf.Min(maxFreq, MAX_SAMPLE_RATE); // Cap at 48kHz
                    // Ensure we don't go below the device minimum
                    requestedSampleRate = Mathf.Max(requestedSampleRate, minFreq);
                }

                ReportHub.Log(ReportCategory.VOICE_CHAT,
                    $"Microphone device '{MicrophoneName}' capabilities - MinFreq: {minFreq}Hz, MaxFreq: {maxFreq}Hz, " +
                    $"Selected: {requestedSampleRate}Hz (Unity output: {unitySampleRate}Hz) - Stack trace: {System.Environment.StackTrace}");

                microphoneAudioClip = Microphone.Start(MicrophoneName, MICROPHONE_LOOP, MICROPHONE_LENGTH_SECONDS, requestedSampleRate);
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"Microphone started with sample rate: {requestedSampleRate}Hz (device optimal) - Stack trace: {System.Environment.StackTrace}");

                if (microphoneAudioClip != null)
                {
                    ReportHub.Log(ReportCategory.VOICE_CHAT,
                        $"Microphone AudioClip created - Actual Frequency: {microphoneAudioClip.frequency}Hz, " +
                        $"Channels: {microphoneAudioClip.channels}, Length: {microphoneAudioClip.length}s - Stack trace: {System.Environment.StackTrace}");

                    if (microphoneAudioClip.frequency != requestedSampleRate)
                    {
                        ReportHub.LogWarning(ReportCategory.VOICE_CHAT,
                            $"Sample rate mismatch! Requested: {requestedSampleRate}Hz, Microphone actual: {microphoneAudioClip.frequency}Hz. " +
                            $"Using actual microphone frequency for audio processing. - Stack trace: {System.Environment.StackTrace}");
                    }

                    actualSampleRate = microphoneAudioClip.frequency;
                }
                else
                {
                    actualSampleRate = requestedSampleRate;
                }
            }

            if (microphoneAudioClip == null)
            {
                ReportHub.LogError(ReportCategory.VOICE_CHAT, "Failed to initialize microphone - AudioClip is null");
                return;
            }

            audioFilter.SetMicrophoneInfo(MicrophoneName, actualSampleRate, MICROPHONE_LENGTH_SECONDS);
            audioFilter.SetMicrophoneClip(microphoneAudioClip);

            audioSource.clip = microphoneAudioClip;
            audioSource.loop = true;
            audioSource.volume = AUDIO_SOURCE_VOLUME;
            audioSource.mute = true; // Start muted
            audioSource.spatialBlend = SPATIAL_BLEND_2D;
            audioSource.panStereo = CENTER_PAN;


            // Initialize RtcAudioSource if needed
            if (initializeRtcAudioSource)
                InitializeOrStartRtcAudioSource(actualSampleRate);

            // Set initial audio filter processing state to disabled
            audioFilter.SetProcessingEnabled(false);

            isMicrophoneInitialized = true;
        }

        private void InitializeOrStartRtcAudioSource(int sampleRate, bool isReconfigure = false)
        {
            if (rtcAudioSource == null)
            {
                try
                {
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"Creating new RtcAudioSource - Current AudioFilter subscribers: {GetAudioFilterSubscriberCount()} - Stack trace: {System.Environment.StackTrace}");
                    rtcAudioSource = RtcAudioSource.CreateForVoiceChat(audioSource, audioFilter, (uint)sampleRate);

                    rtcAudioSource.Start();
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"LiveKit RtcAudioSource created and started successfully for voice chat at {sampleRate}Hz - Subscribers: {GetAudioFilterSubscriberCount()} - Stack trace: {System.Environment.StackTrace}");

                    MicrophoneReady?.Invoke();
                }
                catch (System.Exception ex)
                {
                    ReportHub.LogError(ReportCategory.VOICE_CHAT, $"Failed to create LiveKit RtcAudioSource: {ex.Message} - Stack trace: {System.Environment.StackTrace}");
                }
            }
            else if (!isReconfigure)
            {
                try
                {
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"Starting existing RtcAudioSource - Current AudioFilter subscribers: {GetAudioFilterSubscriberCount()} - Stack trace: {System.Environment.StackTrace}");
                    rtcAudioSource.Start();
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"Existing RtcAudioSource started after microphone reinitialization - Subscribers: {GetAudioFilterSubscriberCount()} - Stack trace: {System.Environment.StackTrace}");

                    MicrophoneReady?.Invoke();
                }
                catch (System.Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Failed to start existing RtcAudioSource: {ex.Message} - Stack trace: {System.Environment.StackTrace}");
                }
            }
        }

        private void EnableMicrophone()
        {
            // Use mute/enable for temporary microphone control during calls
            audioSource.mute = false;  // Allow audio processing - mute state controls local playback
            audioFilter.SetProcessingEnabled(true);  // Enable processing while preserving LiveKit subscribers
            
            // Start RtcAudioSource if it exists
            if (rtcAudioSource != null)
            {
                try
                {
                    rtcAudioSource.Start();
                    ReportHub.Log(ReportCategory.VOICE_CHAT, "RtcAudioSource started for microphone enable");
                }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Failed to start RtcAudioSource: {ex.Message}");
                }
            }
            
            EnabledMicrophone?.Invoke();
        }

        private void DisableMicrophone()
        {
            // Use mute/disable for temporary microphone control during calls
            audioSource.mute = true;
            audioFilter.SetProcessingEnabled(false);  // Disable processing while preserving LiveKit subscribers
            
            // Stop RtcAudioSource if it exists
            if (rtcAudioSource != null)
            {
                try
                {
                    rtcAudioSource.Stop();
                    ReportHub.Log(ReportCategory.VOICE_CHAT, "RtcAudioSource stopped for microphone disable");
                }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Failed to stop RtcAudioSource: {ex.Message}");
                }
            }
            
            DisabledMicrophone?.Invoke();
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
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"Microphone change - Current subscribers: {GetAudioFilterSubscriberCount()} - Stack trace: {System.Environment.StackTrace}");

                audioFilter.ResetProcessor();
                audioFilter.SetProcessingEnabled(false);

                audioSource.Stop();
                audioSource.clip = null;
                Microphone.End(MicrophoneName);
                isMicrophoneInitialized = false;
            }

            // Initialize microphone, skipping RtcAudioSource creation if we're going to reconfigure
            bool willReconfigure = rtcAudioSource != null;
            InitializeMicrophone(initializeRtcAudioSource: !willReconfigure);

            // Handle RtcAudioSource reconfiguration if needed
            if (willReconfigure && isMicrophoneInitialized && microphoneAudioClip != null)
            {
                try
                {
                    int newSampleRate = microphoneAudioClip.frequency;

                    rtcAudioSource.Reconfigure(audioSource, audioFilter, forceChannels: 1, forceSampleRate: (uint)newSampleRate);
                    rtcAudioSource.Start();

                    RtcAudioSourceReconfigured?.Invoke(rtcAudioSource);
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"RtcAudioSource reconfigured and restarted for new microphone '{MicrophoneName}' at {newSampleRate}Hz - Stack trace: {System.Environment.StackTrace}");
                    MicrophoneReady?.Invoke();
                }
                catch (System.Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Failed to reconfigure RtcAudioSource for new microphone: {ex.Message} - Stack trace: {System.Environment.StackTrace}");
                    rtcAudioSource?.Stop();
                    rtcAudioSource?.Dispose();
                    rtcAudioSource = null;

                    InitializeOrStartRtcAudioSource(microphoneAudioClip.frequency);
                }
            }

            // Restore previous talking state if in call
            if (isInCall && wasTalking)
            {
                EnableMicrophone();
            }
        }

        private int GetAudioFilterSubscriberCount()
        {
            return audioFilter?.GetSubscriberCount() ?? 0;
        }
    }
}
