using DCL.UI.ProfileElements;
using MVC;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatMemberListViewItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public delegate void ContextMenuButtonClickedDelegate(ChatMemberListViewItem listItem, Transform buttonPosition, Action OnMenuHide);

        /// <summary>
        ///
        /// </summary>
        public event ContextMenuButtonClickedDelegate ContextMenuButtonClicked;

        [SerializeField]
        private TMP_Text nameText;

        [SerializeField]
        private TMP_Text tagText;

        [SerializeField]
        private ProfilePictureView profilePictureView;

        [SerializeField]
        private TMP_Text connectionStatusText;

        [SerializeField]
        private Button contextMenuButton;

        public string Id { get; set; }

        private bool isContextMenuOpen;

        public string Name
        {
            set => nameText.text = value;
        }

        public string Tag
        {
            set => tagText.text = value;
        }

        public ChatMemberConnectionStatus ConnectionStatus
        {
            set => connectionStatusText.text = value.ToString(); // TODO: Localize this
        }

        public Color NameTextColor
        {
            get => nameText.color;

            set => nameText.color = value;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            contextMenuButton.gameObject.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isContextMenuOpen)
                contextMenuButton.gameObject.SetActive(false);
        }

        public void SetupProfilePicture(ViewDependencies viewDependencies, Color userColor, string faceSnapshotUrl, string userId)
        {
            profilePictureView.SetupWithDependencies(viewDependencies, userColor, faceSnapshotUrl, userId);
        }

        private void Start()
        {
            contextMenuButton.onClick.AddListener(OnContextMenuButtonClicked);
        }

        private void OnContextMenuButtonClicked()
        {
            isContextMenuOpen = true;
            ContextMenuButtonClicked?.Invoke(this, contextMenuButton.transform, OnContextMenuHide);
        }

        private void OnContextMenuHide()
        {
            isContextMenuOpen = false;
            contextMenuButton.gameObject.SetActive(false);
        }
    }
}
