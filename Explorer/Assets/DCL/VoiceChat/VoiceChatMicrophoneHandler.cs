using DCL.Diagnostics;
using DCL.Settings.Settings;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Utility.Multithreading;
using Cysharp.Threading.Tasks;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit;

namespace DCL.VoiceChat
{
    public class VoiceChatMicrophoneHandler : IDisposable
    {
        private const bool MICROPHONE_LOOP = true;
        private const int MICROPHONE_LENGTH_SECONDS = 1; // Unity minimum - cannot be less than 1 second
        private const int MAX_SAMPLE_RATE = 48000; // Cap sample rate for voice chat bandwidth efficiency

        private const float AUDIO_SOURCE_VOLUME = 1f; // Keep volume at 1 so OnAudioFilterRead gets called
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
        private RtcAudioSource rtcAudioSource; // LiveKit audio source for voice chat
        private int currentMicrophoneSampleRate; // Track current sample rate for reconfiguration detection

        private bool isMicrophoneInitialized;
        private bool isInCall;

        private float buttonPressStartTime;

        public bool IsTalking { get; private set; }
        public string MicrophoneName { get; private set; }
        public RtcAudioSource RtcAudioSource => rtcAudioSource;

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
            RtcAudioSourceReconfigured = null;

            // Stop and dispose LiveKit audio source
            rtcAudioSource?.Stop();
            rtcAudioSource?.Dispose();
            rtcAudioSource = null;

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

            // Ensure clean state by clearing any existing audio subscribers
            audioFilter.ResetProcessor();

            // Get Unity's current audio configuration - voice chat adapts to it
            var actualConfig = AudioSettings.GetConfiguration();

            // On macOS, Core Audio may override Unity's settings, so use actual output sample rate
            int unitySampleRate = AudioSettings.outputSampleRate;

            ReportHub.Log(ReportCategory.VOICE_CHAT,
                $"Unity audio config - Configured: {actualConfig.sampleRate}Hz, BufferSize: {actualConfig.dspBufferSize}, " +
                $"Actual Output: {unitySampleRate}Hz");

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            if (actualConfig.sampleRate != unitySampleRate)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT,
                    $"macOS Core Audio override detected - Unity configured: {actualConfig.sampleRate}Hz, " +
                    $"but Core Audio using: {unitySampleRate}Hz. Using actual rate for LiveKit compatibility.");
            }
            else
            {
            ReportHub.Log(ReportCategory.VOICE_CHAT,
                    $"macOS Core Audio matches Unity config: {unitySampleRate}Hz");
            }
#endif

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
            // Get device capabilities to determine optimal microphone sample rate
            int minFreq, maxFreq;
            Microphone.GetDeviceCaps(MicrophoneName, out minFreq, out maxFreq);

            // Use device's preferred sample rate, but cap at 48kHz for voice chat efficiency
            // If device reports specific range, use the minimum of (maxFreq, 48kHz)
            // If device supports any frequency (0,0), default to Unity's output rate or 48kHz
            int microphoneSampleRate;
            if (minFreq == 0 && maxFreq == 0)
            {
                // Device supports any rate - prefer Unity's output rate, fallback to 48kHz
                microphoneSampleRate = (unitySampleRate <= MAX_SAMPLE_RATE) ? unitySampleRate : MAX_SAMPLE_RATE;
            }
            else
            {
                microphoneSampleRate = Mathf.Min(maxFreq, MAX_SAMPLE_RATE); // Cap at 48kHz
                // Ensure we don't go below the device minimum
                microphoneSampleRate = Mathf.Max(microphoneSampleRate, minFreq);
            }

            ReportHub.Log(ReportCategory.VOICE_CHAT,
                $"Microphone device '{MicrophoneName}' capabilities - MinFreq: {minFreq}Hz, MaxFreq: {maxFreq}Hz, " +
                $"Selected: {microphoneSampleRate}Hz (Unity output: {unitySampleRate}Hz)");

            // On macOS, be more conservative with microphone settings
            try
            {
                microphoneAudioClip = Microphone.Start(MicrophoneName, MICROPHONE_LOOP, MICROPHONE_LENGTH_SECONDS, microphoneSampleRate);
                if (microphoneAudioClip == null)
                {
                    ReportHub.LogError(ReportCategory.VOICE_CHAT, "Failed to start microphone on macOS. This may indicate permission issues or device conflicts.");
                    return;
                }
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"Microphone started on macOS with sample rate: {microphoneSampleRate}Hz (device optimal)");

                // Pass microphone info to audio filter for latency optimization
                audioFilter.SetMicrophoneInfo(MicrophoneName, microphoneSampleRate, MICROPHONE_LENGTH_SECONDS);
                audioFilter.SetMicrophoneClip(microphoneAudioClip);
            }
            catch (System.Exception ex)
            {
                ReportHub.LogError(ReportCategory.VOICE_CHAT, $"Microphone initialization failed on macOS: {ex.Message}");
                return;
            }
#else
            MicrophoneName = Microphone.devices[voiceChatSettings.SelectedMicrophoneIndex];

            // Get device capabilities to determine optimal microphone sample rate
            int minFreq, maxFreq;
            Microphone.GetDeviceCaps(MicrophoneName, out minFreq, out maxFreq);

            // Use device's preferred sample rate, but cap at 48kHz for voice chat efficiency
            // If device reports specific range, use the minimum of (maxFreq, 48kHz)
            // If device supports any frequency (0,0), default to Unity's output rate or 48kHz
            int microphoneSampleRate;
            if (minFreq == 0 && maxFreq == 0)
            {
                // Device supports any rate - prefer Unity's output rate, fallback to 48kHz
                microphoneSampleRate = (unitySampleRate <= MAX_SAMPLE_RATE) ? unitySampleRate : MAX_SAMPLE_RATE;
            }
            else
            {
                microphoneSampleRate = Mathf.Min(maxFreq, MAX_SAMPLE_RATE); // Cap at 48kHz
                // Ensure we don't go below the device minimum
                microphoneSampleRate = Mathf.Max(microphoneSampleRate, minFreq);
            }

            ReportHub.Log(ReportCategory.VOICE_CHAT,
                $"Microphone device '{MicrophoneName}' capabilities - MinFreq: {minFreq}Hz, MaxFreq: {maxFreq}Hz, " +
                $"Selected: {microphoneSampleRate}Hz (Unity output: {unitySampleRate}Hz)");

            microphoneAudioClip = Microphone.Start(MicrophoneName, MICROPHONE_LOOP, MICROPHONE_LENGTH_SECONDS, microphoneSampleRate);
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"Microphone started with sample rate: {microphoneSampleRate}Hz (device optimal)");

            // Pass microphone info to audio filter for latency optimization
            audioFilter.SetMicrophoneInfo(MicrophoneName, microphoneSampleRate, MICROPHONE_LENGTH_SECONDS);
            audioFilter.SetMicrophoneClip(microphoneAudioClip);

            // Verify the actual recording sample rate matches what we requested
            if (microphoneAudioClip != null)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT,
                    $"Microphone AudioClip created - Actual Frequency: {microphoneAudioClip.frequency}Hz, " +
                    $"Channels: {microphoneAudioClip.channels}, Length: {microphoneAudioClip.length}s");

                if (microphoneAudioClip.frequency != microphoneSampleRate)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT,
                        $"Sample rate mismatch! Requested: {microphoneSampleRate}Hz, Microphone actual: {microphoneAudioClip.frequency}Hz. " +
                        $"This may cause audio quality issues.");
                }
            }
#endif

            audioSource.clip = microphoneAudioClip;
            audioSource.loop = true;
            audioSource.volume = AUDIO_SOURCE_VOLUME;

            // Force mono audio for voice chat - this ensures we always get mono input to our filter
            // This eliminates the need for channel mixing in the audio filter
            audioSource.spatialBlend = SPATIAL_BLEND_2D;
            audioSource.panStereo = CENTER_PAN;

            audioSource.Play();

            // Create LiveKit RtcAudioSource optimized for voice chat (mono, 1 channel)
            int newSampleRate = microphoneAudioClip.frequency;
            bool needsReconfiguration = rtcAudioSource != null && currentMicrophoneSampleRate != newSampleRate;

            try
            {
                if (needsReconfiguration)
                {
                    // Sample rate changed - reconfigure existing RtcAudioSource
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"Sample rate changed from {currentMicrophoneSampleRate}Hz to {newSampleRate}Hz - reconfiguring RtcAudioSource");
                    rtcAudioSource.Reconfigure(audioSource, audioFilter, forceChannels: 1, forceSampleRate: (uint)newSampleRate);
                    currentMicrophoneSampleRate = newSampleRate;

                    // Notify RoomHandler about the reconfiguration
                    RtcAudioSourceReconfigured?.Invoke(rtcAudioSource);
                    ReportHub.Log(ReportCategory.VOICE_CHAT, "LiveKit RtcAudioSource reconfigured successfully for new sample rate");
                }
                else if (rtcAudioSource == null)
                {
                    // Create new RtcAudioSource
                    rtcAudioSource = RtcAudioSource.CreateForVoiceChat(audioSource, audioFilter, (uint)newSampleRate);
                    currentMicrophoneSampleRate = newSampleRate;

                    // Start RtcAudioSource immediately after creation (not for pausing/unpausing)
                    rtcAudioSource.Start();
                    ReportHub.Log(ReportCategory.VOICE_CHAT, $"LiveKit RtcAudioSource created and started successfully for voice chat at {newSampleRate}Hz");
                }
            }
            catch (System.Exception ex)
            {
                ReportHub.LogError(ReportCategory.VOICE_CHAT, $"Failed to create/reconfigure LiveKit RtcAudioSource: {ex.Message}");
                return;
            }

            audioFilter.enabled = false;

            EnabledMicrophone?.Invoke();
            isMicrophoneInitialized = true;

            MicrophoneReady?.Invoke();
        }

        private void EnableMicrophone()
        {
            // Use mute/enable for temporary microphone control during calls
            // RtcAudioSource.Start() is only for initialization, not pausing
            audioSource.mute = false;  // Allow audio processing - mute state controls local playback
            audioFilter.enabled = true;
            EnabledMicrophone?.Invoke();
        }

        private void DisableMicrophone()
        {
            // Use mute/disable for temporary microphone control during calls
            // RtcAudioSource.Stop() is only for cleanup, not pausing
            audioSource.mute = true;
            audioFilter.enabled = false;
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
                // Stop and dispose LiveKit audio source for microphone change
                if (rtcAudioSource != null)
                {
                    rtcAudioSource.Stop();
                    rtcAudioSource.Dispose();
                    rtcAudioSource = null;
                    ReportHub.Log(ReportCategory.VOICE_CHAT, "Stopped and disposed RtcAudioSource for microphone change");
                }

                // Clear all audio stream subscribers to prevent duplicate streams
                audioFilter.ResetProcessor();
                audioFilter.enabled = false;

                // Then stop and clean up the audio source
                audioSource.Stop();
                audioSource.clip = null;
                Microphone.End(MicrophoneName);
                isMicrophoneInitialized = false;
            }

            // Always reinitialize microphone so it's ready when needed
            InitializeMicrophone();

            // RtcAudioSource reconfiguration is handled in InitializeMicrophone()
            // based on sample rate changes - no additional handling needed here

            // Restore previous talking state if in call
            if (isInCall && wasTalking)
            {
                EnableMicrophone();
            }
        }
    }
}
