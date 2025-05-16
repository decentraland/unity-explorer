using DCL.Settings.Settings;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCL.VoiceChat
{
    public class VoiceChatHandler : IDisposable
    {
        private readonly DCLInput dclInput;
        private readonly VoiceChatSettingsAsset voiceChatSettings;
        private readonly float[] waveData;

        public AudioClip MicrophoneAudioClip;

        private bool isTalkingEnabled;
        private string microphoneName;
        private float buttonPressStartTime;
        private bool isPushToTalk;

        public VoiceChatHandler(DCLInput dclInput, VoiceChatSettingsAsset voiceChatSettings)
        {
            this.dclInput = dclInput;
            this.voiceChatSettings = voiceChatSettings;
            waveData = new float[voiceChatSettings.SampleWindow];

            dclInput.VoiceChat.Talk.performed += OnPressed;
            dclInput.VoiceChat.Talk.canceled += OnReleased;
        }

        private void OnPressed(InputAction.CallbackContext obj)
        {
            buttonPressStartTime = Time.time;
            isPushToTalk = false;

            // Start the microphone immediately when button is pressed
            // If it's a quick press, we'll handle it in OnReleased
            if (!isTalkingEnabled)
                EnableMicrophone();
        }

        private void OnReleased(InputAction.CallbackContext obj)
        {
            float pressDuration = Time.time - buttonPressStartTime;

            // If the button was held for longer than the threshold, treat it as push-to-talk
            if (pressDuration >= voiceChatSettings.HoldThresholdInSeconds)
            {
                isPushToTalk = true;
                if (isTalkingEnabled)
                    DisableMicrophone();
            }
            else
            {
                if (isPushToTalk)
                    return;

                // Handle microphone toggle behaviour
                if (isTalkingEnabled)
                    DisableMicrophone();
            }
        }

        private void EnableMicrophone()
        {
            microphoneName = Microphone.devices[voiceChatSettings.SelectedMicrophoneIndex];
            MicrophoneAudioClip = Microphone.Start(microphoneName, true, 20, AudioSettings.outputSampleRate);
            isTalkingEnabled = true;
        }

        private void DisableMicrophone()
        {
            microphoneName = Microphone.devices[voiceChatSettings.SelectedMicrophoneIndex];
            Microphone.End(microphoneName);
            isTalkingEnabled = false;
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
        }
    }
}
