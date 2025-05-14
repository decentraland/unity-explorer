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

        public VoiceChatHandler(DCLInput dclInput, VoiceChatSettingsAsset voiceChatSettings)
        {
            this.dclInput = dclInput;
            this.voiceChatSettings = voiceChatSettings;
            waveData = new float[voiceChatSettings.SampleWindow];
            dclInput.VoiceChat.Talk.performed += OnPressed;
        }

        private void OnPressed(InputAction.CallbackContext obj)
        {
            //detect different interactions with the button, pressed once or held
            //in order to enable/disable microphone or behave as push to talk
            ToggleVoiceInput();
        }

        private void ToggleVoiceInput()
        {
            microphoneName = Microphone.devices[voiceChatSettings.SelectedMicrophoneIndex];
            if (isTalkingEnabled)
            {
                Microphone.End(microphoneName);
            }
            else
            {
                MicrophoneAudioClip = Microphone.Start(microphoneName, true, 20, AudioSettings.outputSampleRate);
            }

            isTalkingEnabled = !isTalkingEnabled;
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
        }
    }
}
