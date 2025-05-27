using DCL.Settings.Settings;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCL.VoiceChat
{
    public class VoiceChatMicrophoneHandler : IDisposable
    {
        private const bool MICROPHONE_LOOP = true;
        private const int MICROPHONE_LENGTH_SECONDS = 1;
        private const int MICROPHONE_SAMPLE_RATE = 48000;

        public event Action EnabledMicrophone;
        public event Action DisabledMicrophone;

        private readonly DCLInput dclInput;
        private readonly VoiceChatSettingsAsset voiceChatSettings;
        private readonly VoiceChatConfiguration voiceChatConfiguration;
        private readonly AudioSource audioSource;
        private readonly VoiceChatMicrophoneAudioFilter audioFilter;

        private AudioClip microphoneAudioClip;

        private bool isMicrophoneInitialized;

        private float buttonPressStartTime;

        public bool IsTalking { get; private set; }
        public string MicrophoneName { get; private set; }

        public VoiceChatMicrophoneHandler(DCLInput dclInput, VoiceChatSettingsAsset voiceChatSettings, VoiceChatConfiguration voiceChatConfiguration, AudioSource audioSource, VoiceChatMicrophoneAudioFilter audioFilter = null)
        {
            this.dclInput = dclInput;
            this.voiceChatSettings = voiceChatSettings;
            this.voiceChatConfiguration = voiceChatConfiguration;
            this.audioSource = audioSource;
            this.audioFilter = audioFilter;

            dclInput.VoiceChat.Talk.performed += OnPressed;
            dclInput.VoiceChat.Talk.canceled += OnReleased;
            voiceChatSettings.MicrophoneChanged += OnMicrophoneChanged;

            InitializeMicrophone();
        }

        public void Dispose()
        {
            dclInput.VoiceChat.Talk.performed -= OnPressed;
            dclInput.VoiceChat.Talk.canceled -= OnReleased;
            voiceChatSettings.MicrophoneChanged -= OnMicrophoneChanged;

            if (isMicrophoneInitialized)
            {
                audioSource.volume = 0f;
                audioSource.Stop();
                audioSource.clip = null;
                Microphone.End(MicrophoneName);
            }
        }

        private void OnPressed(InputAction.CallbackContext obj)
        {
            buttonPressStartTime = Time.time;

            // Start the microphone immediately when button is pressed
            // If it's a quick press, we'll handle it in OnReleased
            if (!IsTalking)
                EnableMicrophone();
        }

        private void OnReleased(InputAction.CallbackContext obj)
        {
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
            {
                MicrophoneName = Microphone.devices[voiceChatSettings.SelectedMicrophoneIndex];
            }

        public void ToggleMicrophone()
        {
            if(isTalking)
                EnableMicrophone();
            else
                DisableMicrophone();
        }

        private void EnableMicrophone()
        {
            microphoneName = Microphone.devices[voiceChatSettings.SelectedMicrophoneIndex];

            MicrophoneAudioClip = Microphone.Start(microphoneName, true, 5, AudioSettings.outputSampleRate);
            audioSource.clip = MicrophoneAudioClip;
            // On macOS, be more conservative with microphone settings
            try
            {
                microphoneAudioClip = Microphone.Start(MicrophoneName, MICROPHONE_LOOP, MICROPHONE_LENGTH_SECONDS, MICROPHONE_SAMPLE_RATE);
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
            MicrophoneName = Microphone.devices[voiceChatSettings.SelectedMicrophoneIndex];
            microphoneAudioClip = Microphone.Start(MicrophoneName, MICROPHONE_LOOP, MICROPHONE_LENGTH_SECONDS, MICROPHONE_SAMPLE_RATE);
#endif

            audioSource.clip = microphoneAudioClip;
            audioSource.loop = true;
            audioSource.volume = 0f;
            audioSource.Play();
            EnabledMicrophone?.Invoke();
            Debug.Log("Enable microphone");
            isMicrophoneInitialized = true;
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Microphone initialized");
        }

        private void EnableMicrophone()
        {
            if (!isMicrophoneInitialized)
                InitializeMicrophone();

            audioSource.volume = 1f;

            ReportHub.Log(ReportCategory.VOICE_CHAT, "Enable microphone");
        }

        private void DisableMicrophone()
        {
            audioSource.Stop();
            audioSource.clip = null;
            microphoneName = Microphone.devices[voiceChatSettings.SelectedMicrophoneIndex];
            Microphone.End(microphoneName);
            DisabledMicrophone?.Invoke();
            Debug.Log("Disable microphone");
        }
            audioSource.volume = 0f;
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Disable microphone");
        }

        private void OnMicrophoneChanged(int newMicrophoneIndex)
        {
            bool wasTalking = IsTalking;

            if (isMicrophoneInitialized)
            {
                audioSource.Stop();
                audioSource.clip = null;
                Microphone.End(MicrophoneName);
                isMicrophoneInitialized = false;
            }

            audioFilter?.ResetProcessor();

            InitializeMicrophone();

            if (wasTalking)
                audioSource.volume = 1f;

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"Microphone restarted with new device: {Microphone.devices[newMicrophoneIndex]}");
        }
    }
}
