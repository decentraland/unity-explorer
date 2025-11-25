using DG.Tweening;
using System;
using UnityEngine;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatPanelView : MonoBehaviour
    {
        private const float SHOW_HIDE_ANIMATION_DURATION = 0.5f;

        public event Action? OpenListenersSection;
        public event Action? CloseListenersSection;

        [field: SerializeField] private CanvasGroup VoiceChatCanvasGroup { get;  set; }
        [field: SerializeField] private GameObject VoiceChatContainer { get;  set; }
        [field: SerializeField] public CommunityVoiceChatInCallView CommunityVoiceChatInCallView { get; private set; }
        [field: SerializeField] public CommunityVoiceChatSearchView CommunityVoiceChatSearchView { get; private set; }


        private void Start()
        {
            CommunityVoiceChatInCallView.OpenListenersSectionButton.onClick.AddListener(OnOpenListenersSectionClicked);
            CommunityVoiceChatSearchView.BackButton.onClick.AddListener(OnCloseListenersSectionClicked);
        }

        private void OnOpenListenersSectionClicked()
        {
            OpenListenersSection?.Invoke();
        }

        private void OnCloseListenersSectionClicked()
        {
            CloseListenersSection?.Invoke();
        }

        public void Show()
        {
            SetConnectedPanel(false);
            VoiceChatContainer.SetActive(true);
            VoiceChatCanvasGroup
               .DOFade(1, SHOW_HIDE_ANIMATION_DURATION)
               .SetEase(Ease.Flash)
               .OnComplete(() =>
                {
                    VoiceChatCanvasGroup.alpha = 1;
                });
        }

        public void Hide()
        {
            VoiceChatCanvasGroup
               .DOFade(0, SHOW_HIDE_ANIMATION_DURATION)
               .SetEase(Ease.Flash)
               .OnComplete(() =>
                {
                    VoiceChatContainer.SetActive(false);
                    VoiceChatCanvasGroup.alpha = 0;
                });
        }

        public void SetConnectedPanel(bool isConnected)
        {
            CommunityVoiceChatInCallView.ConnectingPanel.SetActive(!isConnected);
            CommunityVoiceChatInCallView.ContentPanel.SetActive(isConnected);
            CommunityVoiceChatInCallView.FooterPanel.SetActive(isConnected);
        }

        private void OnDestroy()
        {
            CommunityVoiceChatInCallView.OpenListenersSectionButton.onClick.RemoveListener(OnOpenListenersSectionClicked);
            CommunityVoiceChatSearchView.BackButton.onClick.RemoveListener(OnCloseListenersSectionClicked);
        }
    }
}
