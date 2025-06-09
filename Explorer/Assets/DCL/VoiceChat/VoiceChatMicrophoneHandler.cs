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
    public class VoiceChatMicrophoneHandler : IDisposable
    {
        internal const bool MICROPHONE_LOOP = true;
        internal const int MICROPHONE_LENGTH_SECONDS = 1;
        internal const int MAX_SAMPLE_RATE = 48000;

        private const float AUDIO_SOURCE_VOLUME = 1f;
        private const float SPATIAL_BLEND_2D = 0f;
        private const float CENTER_PAN = 0f;

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

        private int previousSampleRate = 0;
        private int previousChannels = 0;

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

            if (rtcAudioSource != null)
            {
                try
                {
                    rtcAudioSource.Stop();
                }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Failed to stop RtcAudioSource during dispose: {ex.Message}");
                }

                //rtcAudioSource.Dispose();
                rtcAudioSource = null;
            }

            if (isMicrophoneInitialized)
            {
                if (audioSource != null)
                {
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
                DisableMicrophone();
            else
                EnableMicrophone();

            IsTalking = !IsTalking;
        }

        private void InitializeMicrophone(bool initializeRtcAudioSource = true)
        {
            if (isMicrophoneInitialized)
                return;

            if (Microphone.devices.Length == 0)
            {
                ReportHub.LogError(ReportCategory.VOICE_CHAT, "No microphone devices found. Cannot initialize microphone.");
                return;
            }

            if (voiceChatSettings.SelectedMicrophoneIndex >= Microphone.devices.Length)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Selected microphone index {voiceChatSettings.SelectedMicrophoneIndex} is out of range. Using default microphone.");
                MicrophoneName = Microphone.devices[0];
            }
            else
                MicrophoneName = Microphone.devices[voiceChatSettings.SelectedMicrophoneIndex];

            int minFreq, maxFreq;
            Microphone.GetDeviceCaps(MicrophoneName, out minFreq, out maxFreq);

            int unitySampleRate = AudioSettings.outputSampleRate;
            int requestedSampleRate;
            if (minFreq == 0 && maxFreq == 0)
            {
                requestedSampleRate = (unitySampleRate <= MAX_SAMPLE_RATE) ? unitySampleRate : MAX_SAMPLE_RATE;
            }
            else
            {
                requestedSampleRate = Mathf.Min(maxFreq, MAX_SAMPLE_RATE);
                requestedSampleRate = Mathf.Max(requestedSampleRate, minFreq);
            }

            microphoneAudioClip = Microphone.Start(MicrophoneName, MICROPHONE_LOOP, MICROPHONE_LENGTH_SECONDS, requestedSampleRate);

            int actualSampleRate;
            if (microphoneAudioClip != null)
            {
                if (microphoneAudioClip.frequency != requestedSampleRate)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT,
                        $"Sample rate mismatch! Requested: {requestedSampleRate}Hz, Microphone actual: {microphoneAudioClip.frequency}Hz. Using actual microphone frequency for audio processing.");
                }

                actualSampleRate = microphoneAudioClip.frequency;
            }
            else
            {
                actualSampleRate = requestedSampleRate;
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
            audioSource.spatialBlend = SPATIAL_BLEND_2D;
            audioSource.panStereo = CENTER_PAN;

            if (initializeRtcAudioSource)
                InitializeOrStartRtcAudioSource(actualSampleRate);

            audioFilter.SetProcessingEnabled(false);

            isMicrophoneInitialized = true;

            if (microphoneAudioClip != null)
            {
                previousSampleRate = microphoneAudioClip.frequency;
                previousChannels = microphoneAudioClip.channels;
            }
        }

        private void InitializeOrStartRtcAudioSource(int sampleRate, bool isReconfigure = false)
        {
            if (rtcAudioSource == null)
            {
                try
                {
                    //rtcAudioSource = RtcAudioSource.CreateCustom(audioSource, audioFilter, (uint)sampleRate, 1);
                    rtcAudioSource.Start();
                    MicrophoneReady?.Invoke();
                }
                catch (System.Exception ex)
                {
                    ReportHub.LogError(ReportCategory.VOICE_CHAT, $"Failed to create LiveKit RtcAudioSource: {ex.Message}");
                }
            }
            else if (!isReconfigure)
            {
                try
                {
                    rtcAudioSource.Start();
                    MicrophoneReady?.Invoke();
                }
                catch (System.Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Failed to start existing RtcAudioSource: {ex.Message}");
                }
            }
        }

        private void EnableMicrophone()
        {
            audioFilter.SetProcessingEnabled(true);

            if (rtcAudioSource != null)
            {
                try
                {
                    rtcAudioSource.Start();
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
            audioFilter.SetProcessingEnabled(false);

            if (rtcAudioSource != null)
            {
                try
                {
                    rtcAudioSource.Stop();
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
                audioFilter.ResetProcessor();
                audioFilter.SetProcessingEnabled(false);

                audioSource.Stop();
                audioSource.clip = null;
                Microphone.End(MicrophoneName);
                isMicrophoneInitialized = false;
            }

            bool willReconfigure = rtcAudioSource != null;
            InitializeMicrophone(initializeRtcAudioSource: !willReconfigure);

            if (isMicrophoneInitialized && microphoneAudioClip != null)
            {
                audioFilter.SetMicrophoneInfo(MicrophoneName, microphoneAudioClip.frequency, MICROPHONE_LENGTH_SECONDS);
                audioFilter.SetMicrophoneClip(microphoneAudioClip);
            }

            if (willReconfigure && isMicrophoneInitialized && microphoneAudioClip != null)
            {
                try
                {
                    int newSampleRate = microphoneAudioClip.frequency;
                    int newChannels = microphoneAudioClip.channels;

                    bool specsChanged = (newSampleRate != previousSampleRate) || (newChannels != previousChannels);

                    if (specsChanged)
                    {
                        rtcAudioSource.Stop();
                        //rtcAudioSource.Dispose();
                        rtcAudioSource = null;

                        InitializeOrStartRtcAudioSource(newSampleRate);

                        previousSampleRate = newSampleRate;
                        previousChannels = newChannels;
                    }
                    else
                    {
                        rtcAudioSource.Stop();
                        rtcAudioSource.Start();
                    }

                    if (specsChanged)
                    {
                        RtcAudioSourceReconfigured?.Invoke(rtcAudioSource);
                    }
                    MicrophoneReady?.Invoke();
                }
                catch (System.Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Failed to update RtcAudioSource for new microphone: {ex.Message}");
                    rtcAudioSource?.Stop();
                    //rtcAudioSource?.Dispose();
                    rtcAudioSource = null;

                    InitializeOrStartRtcAudioSource(microphoneAudioClip.frequency);

                    previousSampleRate = microphoneAudioClip.frequency;
                    previousChannels = microphoneAudioClip.channels;
                }
            }

            if (isInCall && wasTalking)
            {
                EnableMicrophone();
            }
        }
    }
}
