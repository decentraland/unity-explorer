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
        private readonly VoiceChatMicrophoneAudioFilter audioFilter;
        private readonly float[] waveData;

        public AudioClip MicrophoneAudioClip;

        private bool isTalking;
        private bool isMicrophoneInitialized;
        private string microphoneName;

        public bool IsTalking => isTalking;
        public string MicrophoneName => microphoneName;
        
        private float buttonPressStartTime;

        public VoiceChatMicrophoneHandler(DCLInput dclInput, VoiceChatSettingsAsset voiceChatSettings, AudioSource audioSource, VoiceChatMicrophoneAudioFilter audioFilter = null)
        {
            this.dclInput = dclInput;
            this.voiceChatSettings = voiceChatSettings;
            this.audioSource = audioSource;
            this.audioFilter = audioFilter;
            waveData = new float[voiceChatSettings.SampleWindow];

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

            #if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            // On macOS, check if we have microphone permission
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[VoiceChat] No microphone devices found on macOS. This may indicate missing microphone permissions.");
                return;
            }
            
            // Validate microphone index for macOS
            if (voiceChatSettings.SelectedMicrophoneIndex >= Microphone.devices.Length)
            {
                Debug.LogWarning($"[VoiceChat] Selected microphone index {voiceChatSettings.SelectedMicrophoneIndex} is out of range on macOS. Using default microphone.");
                // Use first available microphone as fallback
                microphoneName = Microphone.devices[0];
            }
            else
            {
                microphoneName = Microphone.devices[voiceChatSettings.SelectedMicrophoneIndex];
            }
            
            // On macOS, be more conservative with microphone settings
            try
            {
                MicrophoneAudioClip = Microphone.Start(microphoneName, true, 1, 48000);
                if (MicrophoneAudioClip == null)
                {
                    Debug.LogError("[VoiceChat] Failed to start microphone on macOS. This may indicate permission issues or device conflicts.");
                    return;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[VoiceChat] Microphone initialization failed on macOS: {ex.Message}");
                return;
            }
            #else
            microphoneName = Microphone.devices[voiceChatSettings.SelectedMicrophoneIndex];
            MicrophoneAudioClip = Microphone.Start(microphoneName, true, 1, 48000);
            #endif
            
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

            // Reset the audio processor for the new microphone
            // This clears learned noise/speech profiles since different mics have different characteristics
            audioFilter?.ResetProcessor();

            // Initialize with new microphone
            InitializeMicrophone();

            // Restore talking state
            if (wasTalking)
            {
                audioSource.volume = 1f;
            }

            Debug.Log($"Microphone restarted with new device: {Microphone.devices[newMicrophoneIndex]}");
        }

        /// <summary>
        /// Get current microphone loudness for monitoring purposes
        /// Note: Audio processing and noise gating is handled by VoiceChatMicrophoneAudioFilter
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
