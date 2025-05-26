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

        private void EnableMicrophone()
        {
            microphoneName = Microphone.devices[voiceChatSettings.SelectedMicrophoneIndex];
            MicrophoneAudioClip = Microphone.Start(microphoneName, true, 5, AudioSettings.outputSampleRate);
            audioSource.clip = MicrophoneAudioClip;
            audioSource.loop = true;
            audioSource.Play();
            Debug.Log("Enable microphone");
        }

        private void DisableMicrophone()
        {
            audioSource.Stop();
            audioSource.clip = null;
            microphoneName = Microphone.devices[voiceChatSettings.SelectedMicrophoneIndex];
            Microphone.End(microphoneName);
            Debug.Log("Disable microphone");
        }

        private void OnMicrophoneChanged(int newMicrophoneIndex)
        {
            // If we're currently talking, restart the microphone with the new device
            if (isTalking)
            {
                DisableMicrophone();
                EnableMicrophone();
                Debug.Log($"Microphone restarted with new device: {Microphone.devices[newMicrophoneIndex]}");
            }
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
                // Loudness is above threshold, ensure audio is playing
                if (!audioSource.isPlaying)
                {
                    audioSource.Play();
                }
            }
            else
            {
                // Loudness is below threshold, ensure audio is stopped
                if (audioSource.isPlaying)
                {
                    audioSource.Stop();
                }
            }
        }

        private float GetLoudnessFromMicrophone()
        {
            int startPosition = Microphone.GetPosition(microphoneName) - voiceChatSettings.SampleWindow;

            if (startPosition < 0) return 0;

            float totalLoudness = 0;

            MicrophoneAudioClip.GetData(waveData, startPosition);

            for (var i = 0; i < waveData.Length; i++)
                totalLoudness += Mathf.Abs(waveData[i]);

            return totalLoudness / voiceChatSettings.SampleWindow;
        }

        public void Dispose()
        {
            dclInput.VoiceChat.Talk.performed -= OnPressed;
            dclInput.VoiceChat.Talk.canceled -= OnReleased;
            voiceChatSettings.MicrophoneChanged -= OnMicrophoneChanged;
            DisableMicrophone();
        }
    }
}
