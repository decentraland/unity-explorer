using System;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatSearchController : IDisposable
    {
        private readonly CommunityVoiceChatSearchView view;
        private int listenersCount = 0;
        private int requestToSpeakCount = 0;

        public CommunityVoiceChatSearchController(CommunityVoiceChatSearchView view)
        {
            this.view = view;
            view.RequestToSpeakSection.gameObject.SetActive(false);
        }

        public void IncreaseListenerCounter()
        {
            listenersCount++;
            view.ListenersCounter.text = string.Format("({0})", listenersCount);
        }

        public void DecreaseListenerCounter()
        {
            listenersCount--;
            view.ListenersCounter.text = string.Format("({0})", listenersCount);
        }

        public void IncreaseRequestToSpeakCounter()
        {
            requestToSpeakCount++;
            view.RequestToSpeakCounter.text = string.Format("({0})", requestToSpeakCount);

            if (requestToSpeakCount >= 1)
                view.RequestToSpeakSection.gameObject.SetActive(true);
        }

        public void DecreaseRequestToSpeakCounter()
        {
            requestToSpeakCount--;
            view.RequestToSpeakCounter.text = string.Format("({0})", requestToSpeakCount);

            if (requestToSpeakCount == 0)
                view.RequestToSpeakSection.gameObject.SetActive(false);
        }

        public void Dispose()
        {
        }
    }
}
