using DCL.Utilities;
using System;

namespace DCL.VoiceChat.Proximity
{
    public class ProximityVoiceChatButtonController : IDisposable
    {
        private readonly ProximityVoiceChatButtonView view;
        private readonly ReactivePropertyExtensions.DisposableSubscription<ProximityVoiceChatState> stateSubscription;

        public ProximityVoiceChatButtonController(
            ProximityVoiceChatButtonView view,
            ProximityVoiceChatStateModel stateModel)
        {
            this.view = view;

            view.SetState(stateModel.State.Value);
            view.IsBlocked = stateModel.State.Value == ProximityVoiceChatState.Blocked;
            view.CloseAreaButton.onClick.AddListener(view.HideDisabledTooltip);
            stateSubscription = stateModel.State.Subscribe(OnStateChanged);
        }

        private void OnStateChanged(ProximityVoiceChatState state)
        {
            view.SetState(state);
            view.IsBlocked = state == ProximityVoiceChatState.Blocked;

            if (state != ProximityVoiceChatState.Blocked)
                view.HideDisabledTooltip();
        }

        public void Dispose()
        {
            stateSubscription.Dispose();
            view.CloseAreaButton.onClick.RemoveListener(view.HideDisabledTooltip);
            view.HideDisabledTooltip();
        }
    }
}
