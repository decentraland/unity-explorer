using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatTitlebarView : MonoBehaviour
    {
        private const float SHOW_HIDE_ANIMATION_DURATION = 0.5f;

        public event Action CollapseButtonClicked;

        [field: SerializeField]
        public CanvasGroup VoiceChatCanvasGroup { get; private set; }

        [field: SerializeField]
        public GameObject VoiceChatContainer { get; private set; }

        [field: SerializeField]
        public Button CollapseButton  { get; private set; }

        [field: SerializeField]
        public Sprite CollapseButtonImage { get; private set; }

        [field: SerializeField]
        public Sprite UnCollapseButtonImage { get; private set; }

        [field: SerializeField]
        public RectTransform HeaderContainer { get; private set; }

        [field: SerializeField]
        public RectTransform ContentContainer { get; private set; }

        [field: SerializeField]
        public RectTransform FooterContainer { get; private set; }

        [field: SerializeField]
        public CommunityVoiceChatInCallView CommunityVoiceChatInCallView { get; private set; }

        [field: SerializeField]
        public CommunityVoiceChatSearchView CommunityVoiceChatSearchView { get; private set; }

        private void Start()
        {
            CollapseButton.onClick.AddListener(() => CollapseButtonClicked?.Invoke());
        }

        public void SetCollapsedButtonState(bool isCollapsed)
        {
            ContentContainer.gameObject.SetActive(!isCollapsed);
            FooterContainer.gameObject.SetActive(!isCollapsed);
            CollapseButton.image.sprite = isCollapsed ? UnCollapseButtonImage : CollapseButtonImage;
        }

        public void Show()
        {
            VoiceChatContainer.SetActive(true);
            VoiceChatCanvasGroup.alpha = 0;
            VoiceChatCanvasGroup
               .DOFade(1, SHOW_HIDE_ANIMATION_DURATION)
               .SetEase(Ease.Flash)
               .OnComplete(() =>
                {
                    VoiceChatContainer.SetActive(true);
                    VoiceChatCanvasGroup.alpha = 1;
                });
        }

        public void Hide()
        {
            VoiceChatCanvasGroup.alpha = 1;
            VoiceChatCanvasGroup
               .DOFade(0, SHOW_HIDE_ANIMATION_DURATION)
               .SetEase(Ease.Flash)
               .OnComplete(() =>
                {
                    VoiceChatContainer.SetActive(false);
                    VoiceChatCanvasGroup.alpha = 0;
                });
        }
    }
}
