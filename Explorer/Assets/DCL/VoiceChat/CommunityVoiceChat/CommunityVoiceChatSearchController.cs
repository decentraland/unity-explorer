using System;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatSearchController : IDisposable
    {
        private readonly CommunityVoiceChatSearchView view;

        public CommunityVoiceChatSearchController(CommunityVoiceChatSearchView view)
        {
            this.view = view;
            view.RequestToSpeakSection.gameObject.SetActive(false);
        }

        public void RefreshCounters()
        {
            view.ListenersCounter.text = $"({view.ListenersParent.transform.childCount})";
            view.RequestToSpeakCounter.text = $"({view.RequestToSpeakParent.transform.childCount})";
            view.RequestToSpeakSection.gameObject.SetActive(view.RequestToSpeakParent.transform.childCount >= 1);
        }

        public void Dispose()
        {
        }
    }
}
