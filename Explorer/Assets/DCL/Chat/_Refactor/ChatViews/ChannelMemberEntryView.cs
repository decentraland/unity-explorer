using DCL.Chat.ChatViewModels;
using DCL.FeatureFlags;
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
            if (!model.HasProfile)
                return;

            canUnhover = false;

            var request = new MemberEntryContextMenuRequest
            {
                UserId = model.Profile.UserId, Position = contextMenuButton.transform.position,
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
            if (!model.HasProfile)
                return;

            var request = new MemberEntryContextMenuRequest
            {
                UserId = model.Profile.UserId, Position = itemButton.transform.position,
            };
            OnItemSelectRequested?.Invoke(request);
        }

        public void Setup(ChatMemberListViewModel model)
        {
            this.model = model;
            onlineIndicator.SetActive(model.IsOnline);
            profilePictureView.Bind(model.ProfileThumbnail);
            bool isOfficial = OfficialWalletsHelper.Instance.IsOfficialWallet(model.Profile.UserId);
            usernameView.Setup(model.UserName, model.Profile.UserId, model.Profile.HasClaimedName, isOfficial, model.Profile.UserNameColor);

            // Pooled item: every interaction is derived from the bound model so nothing carries over from the previous member
            bool hasProfile = model.HasProfile;
            profilePictureView.ConfigureThumbnailClickData(hasProfile ? HandleContextMenuRequest : null, hasProfile ? model.Profile.UserId.Value : null);
            canUnhover = true;
            Unhover();
        }

        private void Hover() =>
            contextMenuButton.gameObject.SetActive(model.HasProfile);

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
