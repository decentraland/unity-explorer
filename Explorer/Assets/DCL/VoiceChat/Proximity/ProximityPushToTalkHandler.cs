using DCL.Diagnostics;
using DCL.Utilities;
using DCL.VoiceChat.Proximity.UI;
using System;
using UnityEngine.InputSystem;

namespace DCL.VoiceChat.Proximity
{
    public class ProximityPushToTalkHandler : IDisposable
    {
        private readonly ProximityVoiceChatStateModel stateModel;
        private readonly ReactivePropertyExtensions.DisposableSubscription<ProximityVoiceChatState> stateSubscription;

        private bool subscribed;

        public ProximityPushToTalkHandler(ProximityVoiceChatStateModel stateModel)
        {
            this.stateModel = stateModel;

            stateSubscription = stateModel.State.Subscribe(OnStateChanged);
            SyncWithState(stateModel.State.Value);
        }

        public void Dispose()
        {
            stateSubscription.Dispose();
            Unsubscribe();
        }

        private void OnStateChanged(ProximityVoiceChatState state)
        {
            SyncWithState(state);
        }

        private void SyncWithState(ProximityVoiceChatState state)
        {
            switch (state)
            {
                case ProximityVoiceChatState.HEARING:
                case ProximityVoiceChatState.SPEAKING:
                    Subscribe();
                    break;

                case ProximityVoiceChatState.DISABLED:
                case ProximityVoiceChatState.SUPPRESSED:
                    Unsubscribe();
                    break;
            }
        }

        private void Subscribe()
        {
            if (subscribed) return;
            subscribed = true;

            DCLInput.Instance.VoiceChat.Talk!.performed += OnTalkPressed;
            DCLInput.Instance.VoiceChat.Talk.canceled += OnTalkReleased;

            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, "PTT subscribed");
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            subscribed = false;

            DCLInput.Instance.VoiceChat.Talk!.performed -= OnTalkPressed;
            DCLInput.Instance.VoiceChat.Talk.canceled -= OnTalkReleased;

            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, "PTT unsubscribed");
        }

        private void OnTalkPressed(InputAction.CallbackContext ctx)
        {
            stateModel.StartSpeaking();
        }

        private void OnTalkReleased(InputAction.CallbackContext ctx)
        {
            stateModel.StopSpeaking();
        }
    }
}
