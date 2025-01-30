using Cysharp.Threading.Tasks;
using DCL.UI.GenericContextMenu.Controls.Configs;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Utility;

namespace DCL.UI.GenericContextMenu.Controls
{
    public class GenericContextMenuUserProfileView : GenericContextMenuComponentBase
    {
        private const int USER_NAME_MIN_HEIGHT = 20;
        private const int FACE_FRAME_MIN_HEIGHT = 60;
        private const int FRIEND_BUTTON_MIN_HEIGHT = 40;
        private const int COPY_ANIMATION_DURATION = 1000;

        private enum CopyUserInfoSection
        {
            NAME,
            ADDRESS
        }

        [field: SerializeField] public Image FaceFrame { get; private set; }
        [field: SerializeField] public Image FaceRim { get; private set; }
        [field: SerializeField] public ImageView ThumbnailImageView { get; private set; }
        [field: SerializeField] public TMP_Text UserName { get; private set; }
        [field: SerializeField] public TMP_Text UserNameTag { get; private set; }
        [field: SerializeField] public TMP_Text UserAddress { get; private set; }
        [field: SerializeField] public Image ClaimedNameBadge { get; private set; }
        [field: SerializeField] public GameObject ClaimedNameBadgeSeparator { get; private set; }
        [field: SerializeField] public Button CopyNameButton { get; private set; }
        [field: SerializeField] public WarningNotificationView CopyNameToast { get; private set; }
        [field: SerializeField] public Button CopyAddressButton { get; private set; }
        [field: SerializeField] public WarningNotificationView CopyAddressToast { get; private set; }
        [field: SerializeField] public VerticalLayoutGroup ContentVerticalLayout { get; private set; }

        [field: Header("Friendship Button")]
        [field: SerializeField] public Button AddFriendButton { get; private set; }
        [field: SerializeField] public Image AddFriendButtonImage { get; private set; }
        [field: SerializeField] public TMP_Text AddFriendButtonText { get; private set; }

        [field: Space(10)]
        [field: SerializeField] public Sprite AddFriendSprite { get; private set; }
        [field: SerializeField] public string AddFriendText { get; private set; }
        [field: SerializeField] public Sprite AlreadyFriendSprite { get; private set; }
        [field: SerializeField] public string AlreadyFriendText { get; private set; }

        [field: Header("Components cache")]
        [field: SerializeField] private RectTransform userNameRectTransform;
        [field: SerializeField] private RectTransform faceFrameRectTransform;
        [field: SerializeField] private RectTransform userAddressRectTransform;
        [field: SerializeField] private RectTransform addButtonRectTransform;
        [field: SerializeField] private Sprite defaultEmptyThumbnail;

        private CancellationTokenSource copyAnimationCts = new();

        public void Configure(UserProfileContextMenuControlSettings settings)
        {
            HorizontalLayoutComponent.padding = settings.horizontalLayoutPadding;

            ConfigureUserNameAndTag(settings.userName, settings.userAddress, settings.hasClaimedName, settings.userColor);
            ThumbnailImageView.SetImage(settings.userThumbnail ?? defaultEmptyThumbnail);
            ConfigureAddFriendButton(settings.friendshipStatus);

            RectTransformComponent.sizeDelta = new Vector2(RectTransformComponent.sizeDelta.x, CalculateComponentHeight());

            copyAnimationCts = copyAnimationCts.SafeRestart();

            CopyNameButton.onClick.AddListener(() => CopyUserInfo(settings, CopyUserInfoSection.NAME));
            CopyAddressButton.onClick.AddListener(() => CopyUserInfo(settings, CopyUserInfoSection.ADDRESS));
            AddFriendButton.onClick.AddListener(() => settings.requestFriendshipAction(settings.userAddress, settings.friendshipStatus));
        }

        private void CopyUserInfo(UserProfileContextMenuControlSettings settings, CopyUserInfoSection section)
        {
            settings.systemClipboard.Set(section == CopyUserInfoSection.NAME ? settings.userName : settings.userAddress);
            CopyNameAnimationAsync(copyAnimationCts.Token).Forget();

            async UniTaskVoid CopyNameAnimationAsync(CancellationToken ct)
            {
                WarningNotificationView toast = section == CopyUserInfoSection.NAME ? CopyNameToast : CopyAddressToast;
                toast.Show(ct);
                await UniTask.Delay(COPY_ANIMATION_DURATION, cancellationToken: ct);
                toast.Hide(ct: ct);
            }
        }

        private void ConfigureUserNameAndTag(string userName, string userAddress, bool hasClaimedName, Color userColor)
        {
            UserName.text = userName;
            UserName.color = userColor;
            UserNameTag.text = $"#{userAddress[^4..]}";
            UserAddress.text = $"{userAddress[..5]}...{userAddress[^5..]}";

            UserNameTag.gameObject.SetActive(!hasClaimedName);
            ClaimedNameBadge.gameObject.SetActive(hasClaimedName);
            ClaimedNameBadgeSeparator.gameObject.SetActive(hasClaimedName);

            CopyAddressToast.Hide(true);
            CopyNameToast.Hide(true);

            FaceFrame.color = userColor;
            userColor.r += 0.3f;
            userColor.g += 0.3f;
            userColor.b += 0.3f;
            FaceRim.color = userColor;
        }

        private float CalculateComponentHeight()
        {
            float totalHeight = Math.Max(userNameRectTransform.rect.height, USER_NAME_MIN_HEIGHT)
                                + Math.Max(faceFrameRectTransform.rect.height, FACE_FRAME_MIN_HEIGHT)
                                + Math.Max(userAddressRectTransform.rect.height, USER_NAME_MIN_HEIGHT)
                                + HorizontalLayoutComponent.padding.bottom
                                + HorizontalLayoutComponent.padding.top
                                + (ContentVerticalLayout.spacing * 2);
            if (AddFriendButton.gameObject.activeSelf)
                totalHeight += Math.Max(addButtonRectTransform.rect.height, FRIEND_BUTTON_MIN_HEIGHT) + ContentVerticalLayout.spacing;

            return totalHeight;
        }

        private void ConfigureAddFriendButton(UserProfileContextMenuControlSettings.FriendshipStatus friendshipStatus)
        {
            switch (friendshipStatus)
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
        }

        public override void UnregisterListeners()
        {
            CopyNameButton.onClick.RemoveAllListeners();
            CopyAddressButton.onClick.RemoveAllListeners();
            AddFriendButton.onClick.RemoveAllListeners();
            copyAnimationCts.SafeCancelAndDispose();
        }

        public override void RegisterCloseListener(Action listener)
        {
            AddFriendButton.onClick.AddListener(new UnityAction(listener));
        }
    }
}
