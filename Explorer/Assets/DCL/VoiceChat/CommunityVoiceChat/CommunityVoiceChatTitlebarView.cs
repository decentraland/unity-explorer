using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatTitlebarView : MonoBehaviour
    {
        public event Action CollapseButtonClicked;

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
    }
}
