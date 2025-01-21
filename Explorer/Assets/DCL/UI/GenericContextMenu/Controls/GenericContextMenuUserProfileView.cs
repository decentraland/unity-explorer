using DCL.UI.GenericContextMenu.Controls.Configs;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.GenericContextMenu.Controls
{
    public class GenericContextMenuUserProfileView : GenericContextMenuComponentBase
    {
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

        public void Configure(UserProfileContextMenuControlSettings settings)
        {
            HorizontalLayoutComponent.padding = settings.horizontalLayoutPadding;

            UserName.text = settings.profile.Name;
            UserName.color = settings.userColor;
            UserNameTag.text = $"#{settings.profile.UserId[^4..]}";
            UserAddress.text = $"{settings.profile.UserId[..5]}...{settings.profile.UserId[^5..]}";

            UserNameTag.gameObject.SetActive(!settings.profile.HasClaimedName);
            ClaimedNameBadge.gameObject.SetActive(settings.profile.HasClaimedName);

            FaceFrame.color = settings.userColor;
            settings.userColor.r += 0.3f;
            settings.userColor.g += 0.3f;
            settings.userColor.b += 0.3f;
            FaceRim.color = settings.userColor;

            switch (settings.friendshipStatus)
            {
                case UserProfileContextMenuControlSettings.FriendshipStatus.NONE:
                    AddFriendButton.gameObject.SetActive(true);
                    AddFriendButton.interactable = true;
                    AddFriendButtonImage.sprite = AddFriendSprite;
                    AddFriendButtonText.text = AddFriendText;
                    break;
                case UserProfileContextMenuControlSettings.FriendshipStatus.FRIEND:
                    AddFriendButton.gameObject.SetActive(true);
                    AddFriendButton.interactable = false;
                    AddFriendButtonImage.sprite = AlreadyFriendSprite;
                    AddFriendButtonText.text = AlreadyFriendText;
                    break;
                case UserProfileContextMenuControlSettings.FriendshipStatus.BLOCKED:
                    AddFriendButton.gameObject.SetActive(false);
                    break;
                case UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_RECEIVED:
                case UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_SENT:
                    AddFriendButton.gameObject.SetActive(true);
                    AddFriendButton.interactable = false;
                    AddFriendButtonImage.sprite = AddFriendSprite;
                    AddFriendButtonText.text = AddFriendText;
                    break;
            }

            CopyNameButton.onClick.AddListener(() => settings.systemClipboard.Set(settings.profile.Name));
            CopyAddressButton.onClick.AddListener(() => settings.systemClipboard.Set(settings.profile.UserId));
            AddFriendButton.onClick.AddListener(() => settings.requestFriendshipAction(settings.profile));
        }

        public override void UnregisterListeners()
        {
            CopyNameButton.onClick.RemoveAllListeners();
            CopyAddressButton.onClick.RemoveAllListeners();
            AddFriendButton.onClick.RemoveAllListeners();
        }

        public override void RegisterCloseListener(Action listener)
        {
        }
    }
}
