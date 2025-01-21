using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.UI.GenericContextMenu.Controls;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.Friends.ContextMenuComponent
{
    public class GenericContextMenuUserProfileView : GenericContextMenuComponentBase
    {
        [field: SerializeField] public ChatEntryConfigurationSO ChatEntryConfiguration { get; private set; }
        [field: SerializeField] public Image FaceFrame { get; private set; }
        [field: SerializeField] public Image FaceRim { get; private set; }
        [field: SerializeField] public TMP_Text UserName { get; private set; }
        [field: SerializeField] public TMP_Text UserNameTag { get; private set; }
        [field: SerializeField] public TMP_Text UserAddress { get; private set; }
        [field: SerializeField] public Image ClaimedNameBadge { get; private set; }
        [field: SerializeField] public Button CopyNameButton { get; private set; }
        [field: SerializeField] public Button CopyAddressButton { get; private set; }

        [field: Header("Friendship Button")]
        [field: SerializeField] public Button AddFriendButton { get; private set; }
        [field: SerializeField] public Image AddFriendButtonImage { get; private set; }
        [field: SerializeField] public TMP_Text AddFriendButtonText { get; private set; }

        [field: Space(10)]
        [field: SerializeField] public Sprite AddFriendSprite { get; private set; }
        [field: SerializeField] public string AddFriendText { get; private set; }
        [field: SerializeField] public Sprite AlreadyFriendSprite { get; private set; }
        [field: SerializeField] public string AlreadyFriendText { get; private set; }

        private CancellationTokenSource friendshipStatusCts = new ();

        public void Configure(UserProfileContextMenuControlSettings settings)
        {
            HorizontalLayoutComponent.padding = settings.horizontalLayoutPadding;

            Color userColor = ChatEntryConfiguration.GetNameColor(settings.profile.Name);

            UserName.text = settings.profile.Name;
            UserName.color = userColor;
            UserNameTag.text = $"#{settings.profile.UserId[^4..]}";
            UserAddress.text = $"{settings.profile.UserId[..5]}...{settings.profile.UserId[^5..]}";

            UserNameTag.gameObject.SetActive(!settings.profile.HasClaimedName);
            ClaimedNameBadge.gameObject.SetActive(settings.profile.HasClaimedName);

            FaceFrame.color = userColor;
            userColor.r += 0.3f;
            userColor.g += 0.3f;
            userColor.b += 0.3f;
            FaceRim.color = userColor;

            SetFriendShipStatusAsync(settings, friendshipStatusCts.Token).Forget();

            CopyNameButton.onClick.AddListener(() => settings.systemClipboard.Set(settings.profile.Name));
            CopyAddressButton.onClick.AddListener(() => settings.systemClipboard.Set(settings.profile.UserId));
        }

        private async UniTaskVoid SetFriendShipStatusAsync(UserProfileContextMenuControlSettings settings, CancellationToken ct)
        {
            FriendshipStatus friendshipStatus = await settings.friendsService.GetFriendshipStatusAsync(settings.profile.UserId, ct);

            switch (friendshipStatus)
            {
                case FriendshipStatus.NONE:
                    AddFriendButton.gameObject.SetActive(true);
                    AddFriendButton.interactable = true;
                    AddFriendButtonImage.sprite = AddFriendSprite;
                    AddFriendButtonText.text = AddFriendText;
                    break;
                case FriendshipStatus.FRIEND:
                    AddFriendButton.gameObject.SetActive(true);
                    AddFriendButton.interactable = false;
                    AddFriendButtonImage.sprite = AlreadyFriendSprite;
                    AddFriendButtonText.text = AlreadyFriendText;
                    break;
                case FriendshipStatus.BLOCKED:
                    AddFriendButton.gameObject.SetActive(false);
                    break;
                case FriendshipStatus.REQUEST_RECEIVED:
                case FriendshipStatus.REQUEST_SENT:
                    AddFriendButton.gameObject.SetActive(true);
                    AddFriendButton.interactable = false;
                    AddFriendButtonImage.sprite = AddFriendSprite;
                    AddFriendButtonText.text = AddFriendText;
                    break;
            }
        }

        private void OnEnable() =>
            friendshipStatusCts = friendshipStatusCts.SafeRestart();

        private void OnDisable() =>
            friendshipStatusCts.SafeCancelAndDispose();

        public override void UnregisterListeners()
        {
            CopyNameButton.onClick.RemoveAllListeners();
            CopyAddressButton.onClick.RemoveAllListeners();
        }

        public override void RegisterCloseListener(Action listener)
        {
        }
    }
}
