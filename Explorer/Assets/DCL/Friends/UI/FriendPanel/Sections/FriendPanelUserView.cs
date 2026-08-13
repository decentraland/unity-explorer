using Cysharp.Threading.Tasks;
using DCL.FeatureFlags;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.UI.ProfileElements;
using DCL.Utilities;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utility;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public class FriendPanelUserView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        protected readonly List<Button> buttons = new ();

        [field: SerializeField] public Image Background { get; private set; }
        [field: SerializeField] public Color NormalColor { get; private set; }
        [field: SerializeField] public Color HoveredColor { get; private set; }
        [field: SerializeField] public Button MainButton { get; private set; }

        [field: Header("User")]
        [field: SerializeField] public TMP_Text UserName { get; private set; }
        [field: SerializeField] public TMP_Text UserNameTag { get; private set; }
        [field: SerializeField] public GameObject VerifiedIcon { get; private set; }
        [field: SerializeField] public GameObject OfficialIcon { get; private set; }
        [field: SerializeField] public ProfilePictureView ProfilePicture { get; private set; }

        private readonly ReactiveProperty<ProfileThumbnailViewModel> profileThumbnail = new (ProfileThumbnailViewModel.Default());
        private CancellationTokenSource? loadThumbnailCts;

        private bool canUnHover = true;

        public Profile.CompactInfo UserProfile { get; protected set; }

        public event Action<Profile.CompactInfo>? MainButtonClicked;

        internal bool CanUnHover
        {
            get => canUnHover;
            set
            {
                if (!canUnHover && value)
                {
                    canUnHover = value;
                    UnHover();
                }
                canUnHover = value;
            }
        }

        private void Start()
        {
            Background.color = NormalColor;
            MainButton.onClick.AddListener(() => MainButtonClicked?.Invoke(UserProfile));
        }

        private void OnDestroy()
        {
            loadThumbnailCts.SafeCancelAndDispose();
        }

        public void SafelyResetMainButtonListeners()
        {
            MainButton.onClick.RemoveAllListeners();
            MainButton.onClick.AddListener(() => MainButtonClicked?.Invoke(UserProfile));
        }

        public void RemoveMainButtonClickListeners()
        {
            MainButtonClicked = null;
        }

        public virtual void Configure(Profile.CompactInfo friendProfile, ProfileRepositoryWrapper profileDataProvider)
        {
            UnHover();
            UserProfile = friendProfile;

            Color userColor = friendProfile.UserNameColor;

            UserName.text = friendProfile.Name;
            UserName.color = userColor;
            UserNameTag.text = friendProfile.WalletId;
            UserNameTag.gameObject.SetActive(!friendProfile.HasClaimedName);
            VerifiedIcon.SetActive(friendProfile.HasClaimedName);
            OfficialIcon.SetActive(OfficialWalletsHelper.Instance.IsOfficialWallet(friendProfile.UserId));

            profileThumbnail.UpdateValue(ProfileThumbnailViewModel.Default(userColor));
            ProfilePicture.Bind(profileThumbnail);
            loadThumbnailCts = loadThumbnailCts.SafeRestart();
            GetProfileThumbnailCommand.Instance.ExecuteAsync(profileThumbnail, null, friendProfile, loadThumbnailCts.Token).Forget();
        }

        public void ConfigureThumbnailClickData(Action thumbnailContextMenuAction) =>
            ProfilePicture.ConfigureThumbnailClickData(thumbnailContextMenuAction, UserProfile.UserId);

        protected virtual void ToggleButtonView(bool isActive)
        {
            for (int i = 0; i < buttons.Count; i++)
                buttons[i].gameObject.SetActive(isActive);
        }

        private void UnHover()
        {
            ToggleButtonView(false);
            Background.color = NormalColor;
        }

        private void Hover()
        {
            ToggleButtonView(true);
            Background.color = HoveredColor;
        }

        public void OnPointerEnter(PointerEventData eventData) =>
            Hover();

        public void OnPointerExit(PointerEventData eventData)
        {
            if (canUnHover)
                UnHover();
        }
    }
}
