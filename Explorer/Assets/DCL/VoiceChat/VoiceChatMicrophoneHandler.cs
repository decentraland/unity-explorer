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

        public AudioClip MicrophoneAudioClip;

        private bool isTalking;
        private bool isMicrophoneInitialized;
        private string microphoneName;

        public bool IsTalking => isTalking;
        public string MicrophoneName => microphoneName;
        private float buttonPressStartTime;
        private int frameCounter;

        public VoiceChatMicrophoneHandler(DCLInput dclInput, VoiceChatSettingsAsset voiceChatSettings, AudioSource audioSource)
        {
            this.dclInput = dclInput;
            this.voiceChatSettings = voiceChatSettings;
            this.audioSource = audioSource;
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
                
            microphoneName = Microphone.devices[voiceChatSettings.SelectedMicrophoneIndex];
            MicrophoneAudioClip = Microphone.Start(microphoneName, true, 1, AudioSettings.outputSampleRate);
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
            
            // Reset frame counter to ensure immediate loudness checking
            frameCounter = 0;
            
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
            
            // Initialize with new microphone
            InitializeMicrophone();
            
            // Restore talking state
            if (wasTalking)
            {
                audioSource.volume = 1f;
            }
            
            Debug.Log($"Microphone restarted with new device: {Microphone.devices[newMicrophoneIndex]}");
        }

        public void CheckLoudnessAndControlAudio()
        {
            // Only check loudness if we're talking
            if (!isTalking)
                return;

            frameCounter++;

            // Check loudness every X frames as defined in settings
            if (frameCounter >= voiceChatSettings.LoudnessCheckFrameInterval)
            {
                frameCounter = 0;
                CheckMicrophoneLoudnessAndControlAudio();
            }
        }

        private void CheckMicrophoneLoudnessAndControlAudio()
        {
            float currentLoudness = GetLoudnessFromMicrophone();

            if (currentLoudness >= voiceChatSettings.MicrophoneLoudnessMinimumThreshold)
            {
                // Loudness is above threshold, ensure audio is audible
                if (audioSource.volume == 0f)
                {
                    audioSource.volume = 1f;
                }
            }
            else
            {
                // Loudness is below threshold, ensure audio is muted
                if (audioSource.volume > 0f)
                {
                    audioSource.volume = 0f;
                }
            }
        }

        private float GetLoudnessFromMicrophone()
        {
            int micPosition = Microphone.GetPosition(microphoneName);
            
            // Ensure we have enough data recorded before trying to analyze
            if (micPosition < voiceChatSettings.SampleWindow)
                return 0;

            int startPosition = micPosition - voiceChatSettings.SampleWindow;
            
            // Handle wrap-around for circular buffer
            if (startPosition < 0)
                startPosition = 0;

            float totalLoudness = 0;

            MicrophoneAudioClip.GetData(waveData, startPosition);

            // Optimized loop - avoid repeated array access
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
