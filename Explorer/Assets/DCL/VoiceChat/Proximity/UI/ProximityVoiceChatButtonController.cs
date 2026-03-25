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
            stateSubscription = stateModel.State.Subscribe(OnStateChanged);
        }

        private void OnStateChanged(ProximityVoiceChatState state)
        {
            view.SetState(state);
        }

        public void Dispose()
        {
            stateSubscription.Dispose();
        }
    }
}
