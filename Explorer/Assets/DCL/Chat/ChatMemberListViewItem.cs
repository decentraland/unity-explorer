using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatMemberListViewItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public delegate void ContextMenuButtonClickedDelegate(ChatMemberListViewItem listItem, Transform buttonPosition);

        /// <summary>
        ///
        /// </summary>
        public event ContextMenuButtonClickedDelegate ContextMenuButtonClicked;

        [SerializeField]
        private TMP_Text nameText;

        [SerializeField]
        private TMP_Text tagText;

        [SerializeField]
        private Image profilePicture;

        [SerializeField]
        private Image profilePictureBackground;

        [SerializeField]
        private TMP_Text connectionStatusText;

        [SerializeField]
        private Button contextMenuButton;

        public string Id { get; set; }

        public string Name
        {
            set => nameText.text = value;
        }

        public string Tag
        {
            set => tagText.text = value;
        }

        public Sprite ProfilePicture
        {
            set => profilePicture.sprite = value;
        }

        public ChatMemberConnectionStatus ConnectionStatus
        {
            set => connectionStatusText.text = value.ToString(); // TODO: Localize this
        }

        public Color ProfileColor
        {
            get => profilePictureBackground.color;

            set
            {
                nameText.color = value;
                profilePictureBackground.color = value;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            contextMenuButton.gameObject.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            contextMenuButton.gameObject.SetActive(false);
        }

        private void Start()
        {
            contextMenuButton.onClick.AddListener(OnContextMenuButtonClicked);
        }

        private void OnContextMenuButtonClicked()
        {
            ContextMenuButtonClicked?.Invoke(this, contextMenuButton.transform);
        }
    }
}
