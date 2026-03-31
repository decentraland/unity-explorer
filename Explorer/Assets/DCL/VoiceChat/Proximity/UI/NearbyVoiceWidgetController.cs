using DCL.Utilities;
using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

namespace DCL.VoiceChat.Proximity
{
    public class NearbyVoiceWidgetController : IDisposable
    {
        private const string VOLUME_PARAM = "ProximityVoiceChat_Volume";
        private const float MIN_VOLUME_DB = -80f;
        private const string HEARING_TEXT = "Hold <color=#A09BA8>[T]</color> to speak momentarily";
        private const string SPEAKING_PUSH_TO_TALK_TEXT = "Release <color=#A09BA8>[T]</color> to stop speaking";
        private const string SPEAKING_BUTTON_TEXT = "Press <color=#A09BA8>[T]</color> to stop speaking";

        private readonly NearbyVoiceWidgetView view;
        private readonly ProximityVoiceChatStateModel stateModel;
        private readonly AudioMixerGroup? proximityMixerGroup;
        private readonly ReactivePropertyExtensions.DisposableSubscription<ProximityVoiceChatState> stateSubscription;

        private bool pushToTalkSubscribed;

        public NearbyVoiceWidgetController(
            NearbyVoiceWidgetView view,
            ProximityVoiceChatStateModel stateModel,
            AudioMixerGroup? proximityMixerGroup)
        {
            this.view = view;
            this.stateModel = stateModel;
            this.proximityMixerGroup = proximityMixerGroup;

            stateSubscription = stateModel.State.Subscribe(OnStateChanged);
            SyncViewWithState(stateModel.State.Value);

            view.HearOthersToggle.onValueChanged.AddListener(OnHearOthersToggled);
            view.SpeakButton.onClick.AddListener(OnSpeakButtonClicked);
            view.VolumeSlider.onValueChanged.AddListener(OnVolumeChanged);

            SyncSliderWithMixer();
        }

        public void Dispose()
        {
            stateSubscription.Dispose();
            view.HearOthersToggle.onValueChanged.RemoveListener(OnHearOthersToggled);
            view.SpeakButton.onClick.RemoveListener(OnSpeakButtonClicked);
            view.VolumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
            UnsubscribePushToTalk();
        }

        private void OnStateChanged(ProximityVoiceChatState state)
        {
            SyncViewWithState(state);
        }

        private void SyncViewWithState(ProximityVoiceChatState state)
        {
            switch (state)
            {
                case ProximityVoiceChatState.Blocked or ProximityVoiceChatState.Disconnected:
                    UnsubscribePushToTalk();
                    view.CloseAreaButton.onClick.Invoke();
                    break;
                case ProximityVoiceChatState.Hearing:
                    SubscribePushToTalk(); break;
            }

            bool isSpeaking = state == ProximityVoiceChatState.Speaking;
            bool isConnected = isSpeaking || state is ProximityVoiceChatState.Hearing;

            view.HearOthersToggle.SetIsOnWithoutNotify(isConnected);
            view.VolumeSliderContainer.SetActive(isConnected);
            view.SpeakButtonContainer.SetActive(isConnected);
            view.SpeakStateVisuals.SetActive(isConnected && !isSpeaking);
            view.SpeakingStateVisuals.SetActive(isSpeaking);
            view.SetSpeaking(isSpeaking);
            view.HearText.gameObject.SetActive(isConnected);

            if (!isSpeaking)
                view.HearText.text = HEARING_TEXT;
        }

        private void OnHearOthersToggled(bool isOn)
        {
            if (isOn)
                stateModel.Enable();
            else
                stateModel.Disable();
        }

        private void OnSpeakButtonClicked()
        {
            if (stateModel.State.Value == ProximityVoiceChatState.Speaking)
                stateModel.StopSpeaking();
            else if (stateModel.State.Value == ProximityVoiceChatState.Hearing)
            {
                stateModel.StartSpeaking();
                view.HearText.text = SPEAKING_BUTTON_TEXT;
            }
        }

        private void SubscribePushToTalk()
        {
            if (pushToTalkSubscribed) return;
            pushToTalkSubscribed = true;

            DCLInput.Instance.VoiceChat.Talk!.performed += OnPushToTalkPressed;
            DCLInput.Instance.VoiceChat.Talk.canceled += OnPushToTalkReleased;
        }

        private void UnsubscribePushToTalk()
        {
            if (!pushToTalkSubscribed) return;
            pushToTalkSubscribed = false;

            DCLInput.Instance.VoiceChat.Talk!.performed -= OnPushToTalkPressed;
            DCLInput.Instance.VoiceChat.Talk.canceled -= OnPushToTalkReleased;
        }

        private void OnPushToTalkPressed(InputAction.CallbackContext ctx)
        {
            view.HearText.text = SPEAKING_PUSH_TO_TALK_TEXT;
            stateModel.StartSpeaking();
        }

        private void OnPushToTalkReleased(InputAction.CallbackContext ctx)
        {
            stateModel.StopSpeaking();
        }

        private void OnVolumeChanged(float value)
        {
            ApplySliderVolume(value);
        }

        private void SyncSliderWithMixer()
        {
            if (proximityMixerGroup != null &&
                proximityMixerGroup.audioMixer.GetFloat(VOLUME_PARAM, out float db))
            {
                float linear = db > MIN_VOLUME_DB
                    ? Mathf.Pow(10f, db / 20f)
                    : 0f;

                view.VolumeSlider.SetValueWithoutNotify(linear);
            }
        }

        private void ApplySliderVolume(float sliderValue)
        {
            if (proximityMixerGroup == null) return;

            float db = sliderValue > 0.0001f
                ? Mathf.Log10(sliderValue) * 20f
                : MIN_VOLUME_DB;

            proximityMixerGroup.audioMixer.SetFloat(VOLUME_PARAM, db);
        }
    }
}
