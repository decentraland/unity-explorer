using UnityEngine;
using UnityEngine.UI;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatTitlebarView : MonoBehaviour
    {
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

        private void Start()
        {
            CollapseButton.onClick.AddListener(OnCollapseButtonClick);
        }

        private void OnCollapseButtonClick()
        {
            ContentContainer.gameObject.SetActive(!ContentContainer.gameObject.activeSelf);
            FooterContainer.gameObject.SetActive(!FooterContainer.gameObject.activeSelf);
            CollapseButton.image.sprite = ContentContainer.gameObject.activeSelf ? CollapseButtonImage : UnCollapseButtonImage;
        }
    }
}
