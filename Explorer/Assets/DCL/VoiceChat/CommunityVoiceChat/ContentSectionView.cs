using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class ContentSectionView : MonoBehaviour
    {
        [field: SerializeField]
        public Button CollapseButton  { get; private set; }

        [field: SerializeField]
        public Sprite CollapseButtonImage { get; private set; }

        [field: SerializeField]
        public Sprite UnCollapseButtonImage { get; private set; }

        [field: SerializeField]
        public RectTransform ContentContainer  { get; private set; }

        private void Start()
        {
            CollapseButton.onClick.AddListener(OnCollapseButtonClick);
        }

        private void OnCollapseButtonClick()
        {
            ContentContainer.gameObject.SetActive(!ContentContainer.gameObject.activeSelf);
            CollapseButton.image.sprite = ContentContainer.gameObject.activeSelf ? CollapseButtonImage : UnCollapseButtonImage;
        }
    }
}
