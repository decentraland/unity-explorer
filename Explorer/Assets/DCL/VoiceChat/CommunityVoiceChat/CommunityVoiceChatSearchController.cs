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
            view.ListenersCounter.text = string.Format("({0})", view.ListenersParent.transform.childCount);
            view.RequestToSpeakCounter.text = string.Format("({0})", view.RequestToSpeakParent.transform.childCount);
            view.RequestToSpeakSection.gameObject.SetActive(view.RequestToSpeakParent.transform.childCount >= 1);
        }

        public void Dispose()
        {
        }
    }
}
