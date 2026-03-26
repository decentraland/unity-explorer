using DCL.Utilities;
using System;

namespace DCL.VoiceChat.Proximity
{
    public class NearbyVoiceWidgetController : IDisposable
    {
        private readonly NearbyVoiceWidgetView view;
        private readonly ProximityVoiceChatStateModel stateModel;
        private readonly ReactivePropertyExtensions.DisposableSubscription<ProximityVoiceChatState> stateSubscription;

        public NearbyVoiceWidgetController(
            NearbyVoiceWidgetView view,
            ProximityVoiceChatStateModel stateModel)
        {
            this.view = view;
            this.stateModel = stateModel;

            stateSubscription = stateModel.State.Subscribe(OnStateChanged);
            SyncViewWithState(stateModel.State.Value);

            view.HearOthersToggle.onValueChanged.AddListener(OnHearOthersToggled);
            view.SpeakButton.onClick.AddListener(OnSpeakButtonClicked);
        }

        public void Dispose()
        {
            stateSubscription.Dispose();
            view.HearOthersToggle.onValueChanged.RemoveListener(OnHearOthersToggled);
            view.SpeakButton.onClick.RemoveListener(OnSpeakButtonClicked);
        }

        private void OnStateChanged(ProximityVoiceChatState state)
        {
            SyncViewWithState(state);
        }

        private void SyncViewWithState(ProximityVoiceChatState state)
        {
            bool isConnected = state is ProximityVoiceChatState.Hearing or ProximityVoiceChatState.Speaking;

            view.HearOthersToggle.SetIsOnWithoutNotify(isConnected);
            view.VolumeSlider.interactable = isConnected;
            view.SpeakButton.interactable = isConnected;
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
                stateModel.StartSpeaking();
        }
    }
}
