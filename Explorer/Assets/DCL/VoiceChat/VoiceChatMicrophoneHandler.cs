using DCL.Settings.Settings;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCL.VoiceChat
{
    public class VoiceChatMicrophoneHandler : IDisposable
    {
        private readonly DCLInput dclInput;
        private readonly VoiceChatSettingsAsset voiceChatSettings;
        private readonly AudioSource audioSource;
        private readonly float[] waveData;
        private readonly VoiceChatAudioProcessor audioProcessor;

        public AudioClip MicrophoneAudioClip;

        private bool isTalking;
        private bool isMicrophoneInitialized;
        private string microphoneName;

                public bool IsTalking => isTalking;
        public string MicrophoneName => microphoneName;
        public bool IsNoiseGateOpen => audioProcessor?.IsGateOpen ?? false;
        public float CurrentGain => audioProcessor?.CurrentGain ?? 1f;
        public float NoiseFloor => audioProcessor?.NoiseFloor ?? 0f;
        public float SpeechFloor => audioProcessor?.SpeechFloor ?? 0f;
        public bool IsLearningNoise => audioProcessor?.IsLearningNoise ?? false;
        public float AdaptiveThreshold => audioProcessor?.AdaptiveThreshold ?? 0f;
        public float GateSmoothing => audioProcessor?.GateSmoothing ?? 0f;
        
        private float buttonPressStartTime;

        public VoiceChatMicrophoneHandler(DCLInput dclInput, VoiceChatSettingsAsset voiceChatSettings, AudioSource audioSource)
        {
            this.dclInput = dclInput;
            this.voiceChatSettings = voiceChatSettings;
            this.audioSource = audioSource;
            waveData = new float[voiceChatSettings.SampleWindow];
            audioProcessor = new VoiceChatAudioProcessor(voiceChatSettings);

            dclInput.VoiceChat.Talk.performed += OnPressed;
            dclInput.VoiceChat.Talk.canceled += OnReleased;
            voiceChatSettings.MicrophoneChanged += OnMicrophoneChanged;

            // Pre-initialize microphone for faster response
            InitializeMicrophone();
        }

        private void OnPressed(InputAction.CallbackContext obj)
        {
            buttonPressStartTime = Time.time;
            // Start the microphone immediately when button is pressed
            // If it's a quick press, we'll handle it in OnReleased
            if (!isTalking)
                EnableMicrophone();
        }

        private void OnReleased(InputAction.CallbackContext obj)
        {
            float pressDuration = Time.time - buttonPressStartTime;

            // If the button was held for longer than the threshold, treat it as push-to-talk and stop communication on release
            if (pressDuration >= voiceChatSettings.HoldThresholdInSeconds)
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

        private void InitializeMicrophone()
        {
            if (isMicrophoneInitialized)
                return;

            microphoneName = Microphone.devices[voiceChatSettings.SelectedMicrophoneIndex];
            MicrophoneAudioClip = Microphone.Start(microphoneName, true, 1, 48000);
            audioSource.clip = MicrophoneAudioClip;
            audioSource.loop = true;
            audioSource.volume = 0f; // Start muted
            audioSource.Play(); // Start playing immediately but muted
            isMicrophoneInitialized = true;
            Debug.Log("Microphone initialized");
        }

        private void EnableMicrophone()
        {
            if (!isMicrophoneInitialized)
                InitializeMicrophone();

            audioSource.volume = 1f; // Unmute instead of starting playback

            Debug.Log("Enable microphone");
        }

        private void DisableMicrophone()
        {
            audioSource.volume = 0f; // Mute instead of stopping playback
            Debug.Log("Disable microphone");
        }

        private void OnMicrophoneChanged(int newMicrophoneIndex)
        {
            bool wasTalking = isTalking;

            // Stop current microphone
            if (isMicrophoneInitialized)
            {
                audioSource.Stop();
                audioSource.clip = null;
                Microphone.End(microphoneName);
                isMicrophoneInitialized = false;
            }

            // Reset audio processor for new microphone
            audioProcessor.Reset();

            // Initialize with new microphone
            InitializeMicrophone();

            // Restore talking state
            if (wasTalking)
            {
                audioSource.volume = 1f;
            }

            Debug.Log($"Microphone restarted with new device: {Microphone.devices[newMicrophoneIndex]}");
        }

        // Note: Loudness checking and audio control is now handled by VoiceChatAudioProcessor
        // The noise gate in the audio processor provides superior control with timing and smooth transitions

        /// <summary>
        /// Get current microphone loudness for monitoring purposes
        /// Note: Audio processing and noise gating is handled by VoiceChatAudioProcessor
        /// </summary>
        public float GetCurrentLoudness()
        {
            if (!isMicrophoneInitialized || string.IsNullOrEmpty(microphoneName))
                return 0f;

            int micPosition = Microphone.GetPosition(microphoneName);
            
            // Ensure we have enough data recorded before trying to analyze
            if (micPosition < voiceChatSettings.SampleWindow)
                return 0;

            int startPosition = micPosition - voiceChatSettings.SampleWindow;
            
            // Handle wrap-around for circular buffer
            if (startPosition < 0)
                startPosition = 0;

            MicrophoneAudioClip.GetData(waveData, startPosition);

            float totalLoudness = 0;

            // Calculate raw loudness (without processing for monitoring purposes)
            for (int i = 0; i < waveData.Length; i++)
                totalLoudness += Mathf.Abs(waveData[i]);

            return totalLoudness / voiceChatSettings.SampleWindow;
        }

        public void Dispose()
        {
            dclInput.VoiceChat.Talk.performed -= OnPressed;
            dclInput.VoiceChat.Talk.canceled -= OnReleased;
            voiceChatSettings.MicrophoneChanged -= OnMicrophoneChanged;

            if (isMicrophoneInitialized)
            {
                audioSource.Stop();
                audioSource.clip = null;
                Microphone.End(microphoneName);
            }
        }
    }
}
