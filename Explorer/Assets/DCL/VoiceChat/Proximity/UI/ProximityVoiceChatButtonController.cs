namespace DCL.VoiceChat.Proximity
{
    public class ProximityVoiceChatButtonController
    {
        private readonly ProximityVoiceChatButtonView view;

        public ProximityVoiceChatButtonController(ProximityVoiceChatButtonView view)
        {
            this.view = view;
            view.SetState(ProximityVoiceChatState.Hearing);
        }
    }
}
