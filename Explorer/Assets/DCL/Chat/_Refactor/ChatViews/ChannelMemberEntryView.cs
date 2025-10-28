using DCL.Chat.ChatViewModels;
using DCL.UI.ProfileElements;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Chat.ChatViews
{
    public struct MemberEntryContextMenuRequest
    {
        public string UserId;
        public Vector3 Position;
        public Action OnHide;
    }

    public class ChannelMemberEntryView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action<MemberEntryContextMenuRequest> OnContextMenuRequested;
        public event Action<MemberEntryContextMenuRequest> OnItemSelectRequested;

        [Header("UI References")]
        [SerializeField] private TMP_Text userNameText;
        [SerializeField] private ProfilePictureView profilePictureView;
        [SerializeField] private ChatUsernameView usernameView;
        [SerializeField] private GameObject onlineIndicator;
        [SerializeField] private Button contextMenuButton;
        [SerializeField] private Button itemButton;

        private ChatMemberListViewModel model;
        private bool canUnhover = true;

        private void Awake()
        {
            contextMenuButton.onClick.AddListener(HandleContextMenuRequest);
            itemButton.onClick.AddListener(HandleItemContextMenuRequest);
        }

        private void HandleContextMenuRequest()
        {
            canUnhover = false;

            var request = new MemberEntryContextMenuRequest
            {
                UserId = model.UserId, Position = contextMenuButton.transform.position,
                OnHide = () =>
                {
                    canUnhover = true;
                    Unhover();
                }
            };
            OnContextMenuRequested?.Invoke(request);
        }

        private void HandleItemContextMenuRequest()
        {
            var request = new MemberEntryContextMenuRequest
            {
                UserId = model.UserId, Position = itemButton.transform.position
            };
            OnItemSelectRequested?.Invoke(request);
        }

        public void Setup(ChatMemberListViewModel model)
        {
            this.model = model;
            onlineIndicator.SetActive(model.IsOnline);
            profilePictureView.Bind(model.ProfileThumbnail, model.ProfileColor);
            usernameView.Setup(model.UserName, model.UserId, model.HasClaimedName, model.ProfileColor);
        }

        private void Hover() =>
            contextMenuButton.gameObject.SetActive(true);

        private void Unhover() =>
            contextMenuButton.gameObject.SetActive(false);

        public void OnPointerEnter(PointerEventData eventData) =>
            Hover();

        public void OnPointerExit(PointerEventData eventData)
        {
            if (canUnhover)
                Unhover();
        }
    }
}
