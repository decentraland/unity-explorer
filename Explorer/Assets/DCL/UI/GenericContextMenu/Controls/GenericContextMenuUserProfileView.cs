using Cysharp.Threading.Tasks;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using MVC;
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
            ADDRESS,
        }

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
        [field: SerializeField] public ProfilePictureView ProfilePictureView { get; private set; }
        [field: SerializeField] public GameObject FriendsButtonsContainer { get; private set; }

        [field: Header("Friendship Button")]
        [field: SerializeField] public Button AddFriendButton { get; private set; }
        [field: SerializeField] public Button AcceptFriendButton { get; private set; }
        [field: SerializeField] public Button RemoveFriendButton { get; private set; }
        [field: SerializeField] public Button CancelFriendButton { get; private set; }

        [field: Header("Components cache")]
        [field: SerializeField] private RectTransform userNameRectTransform;
        [field: SerializeField] private RectTransform faceFrameRectTransform;
        [field: SerializeField] private RectTransform userAddressRectTransform;
        [field: SerializeField] private RectTransform buttonContainerRectTransform;
        [field: SerializeField] private Sprite defaultEmptyThumbnail;

        private CancellationTokenSource copyAnimationCts = new ();
        private ProfileRepositoryWrapper profileRepositoryWrapper;

        public override void UnregisterListeners()
        {
            CopyNameButton.onClick.RemoveAllListeners();
            CopyAddressButton.onClick.RemoveAllListeners();
            AddFriendButton.onClick.RemoveAllListeners();
            AcceptFriendButton.onClick.RemoveAllListeners();
            RemoveFriendButton.onClick.RemoveAllListeners();
            CancelFriendButton.onClick.RemoveAllListeners();
            copyAnimationCts.SafeCancelAndDispose();
        }

        public override void RegisterCloseListener(Action listener)
        {
            AddFriendButton.onClick.AddListener(new UnityAction(listener));
            AcceptFriendButton.onClick.AddListener(new UnityAction(listener));
            RemoveFriendButton.onClick.AddListener(new UnityAction(listener));
            CancelFriendButton.onClick.AddListener(new UnityAction(listener));
        }

        public void SetProfileDataProvider(ProfileRepositoryWrapper profileDataProvider)
        {
            profileRepositoryWrapper = profileDataProvider;
        }

        public void Configure(UserProfileContextMenuControlSettings settings)
        {
            HorizontalLayoutComponent.padding = settings.horizontalLayoutPadding;

            ConfigureUserNameAndTag(settings.userData.userName, settings.userData.userAddress, settings.userData.hasClaimedName, settings.userData.userColor, settings.showWalletSection);

            if (settings.showProfilePicture)
            {
                ProfilePictureView.gameObject.SetActive(true);
                ProfilePictureView.Setup(profileRepositoryWrapper, settings.userData.userColor, settings.userData.userThumbnailAddress);
            }
            else
                ProfilePictureView.gameObject.SetActive(false);

            ConfigureFriendshipButton(settings);

            RectTransformComponent.sizeDelta = new Vector2(RectTransformComponent.sizeDelta.x, CalculateComponentHeight());

            copyAnimationCts = copyAnimationCts.SafeRestart();

            CopyNameButton.onClick.AddListener(() => CopyUserInfo(settings, CopyUserInfoSection.NAME));
            CopyAddressButton.onClick.AddListener(() => CopyUserInfo(settings, CopyUserInfoSection.ADDRESS));
        }

        private void InvokeSettingsAction(UserProfileContextMenuControlSettings settings) =>
            settings.friendButtonClickAction(settings.userData, settings.friendshipStatus);

        private void CopyUserInfo(UserProfileContextMenuControlSettings settings, CopyUserInfoSection section)
        {
            ViewDependencies.ClipboardManager.Copy(this, section == CopyUserInfoSection.NAME ? settings.userData.userName : settings.userData.userAddress);
            CopyNameAnimationAsync(copyAnimationCts.Token).Forget();

            async UniTaskVoid CopyNameAnimationAsync(CancellationToken ct)
            {
                WarningNotificationView toast = section == CopyUserInfoSection.NAME ? CopyNameToast : CopyAddressToast;
                toast.Show(ct);
                await UniTask.Delay(COPY_ANIMATION_DURATION, cancellationToken: ct);
                toast.Hide(ct: ct);
            }
        }

        private void ConfigureUserNameAndTag(string userName, string userAddress, bool hasClaimedName, Color userColor, bool showWalletSection)
        {
            UserName.text = userName;
            UserName.color = userColor;
            UserNameTag.text = $"#{userAddress[^4..]}";

            if (!showWalletSection)
            {
                UserAddress.gameObject.SetActive(false);
                CopyAddressButton.gameObject.SetActive(false);
            }
            else
            {
                UserAddress.gameObject.SetActive(true);
                UserAddress.text = $"{userAddress[..5]}...{userAddress[^5..]}";
                CopyAddressButton.gameObject.SetActive(true);
            }

            UserNameTag.gameObject.SetActive(!hasClaimedName);
            ClaimedNameBadge.gameObject.SetActive(hasClaimedName);
            ClaimedNameBadgeSeparator.gameObject.SetActive(hasClaimedName);

            CopyAddressToast.Hide(true);
            CopyNameToast.Hide(true);
        }

        private float CalculateComponentHeight()
        {
            float totalHeight = Math.Max(userNameRectTransform.rect.height, USER_NAME_MIN_HEIGHT)
                                + HorizontalLayoutComponent.padding.bottom
                                + HorizontalLayoutComponent.padding.top
                                + ContentVerticalLayout.spacing;

            if (UserAddress.isActiveAndEnabled)
                totalHeight += Math.Max(userAddressRectTransform.rect.height, USER_NAME_MIN_HEIGHT) + ContentVerticalLayout.spacing;

            if (ProfilePictureView.gameObject.activeSelf)
                totalHeight += Math.Max(faceFrameRectTransform.rect.height, FACE_FRAME_MIN_HEIGHT);

            if (AddFriendButton.gameObject.activeSelf || AcceptFriendButton.gameObject.activeSelf || RemoveFriendButton.gameObject.activeSelf || CancelFriendButton.gameObject.activeSelf)
                totalHeight += Math.Max(buttonContainerRectTransform.rect.height, FRIEND_BUTTON_MIN_HEIGHT) + ContentVerticalLayout.spacing;

            return totalHeight;
        }

        private void ConfigureFriendshipButton(UserProfileContextMenuControlSettings settings)
        {
            if (settings.friendshipStatus == UserProfileContextMenuControlSettings.FriendshipStatus.DISABLED)
            {
                FriendsButtonsContainer.gameObject.SetActive(false);
                return;
            }

            FriendsButtonsContainer.gameObject.SetActive(true);

            AddFriendButton.gameObject.SetActive(false);
            RemoveFriendButton.gameObject.SetActive(false);
            CancelFriendButton.gameObject.SetActive(false);
            AcceptFriendButton.gameObject.SetActive(false);

            Button buttonToActivate = settings.friendshipStatus switch
                                      {
                                          UserProfileContextMenuControlSettings.FriendshipStatus.NONE => AddFriendButton,
                                          UserProfileContextMenuControlSettings.FriendshipStatus.FRIEND => RemoveFriendButton,
                                          UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_SENT => CancelFriendButton,
                                          UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_RECEIVED => AcceptFriendButton,
                                          _ => null,
                                      };

            buttonToActivate?.gameObject.SetActive(true);
            buttonToActivate?.onClick.AddListener(() => InvokeSettingsAction(settings));
        }
    }
}
