using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.FeatureFlags;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.UI.ProfileElements;
using DCL.Utilities;
using DG.Tweening;
using MVC;
using System.Threading;
using TMPro;
using UnityEngine;
using Utility;

namespace DCL.Friends.UI.PushNotifications
{
    public class FriendPushNotificationView : ViewBase, IView
    {
        [field: SerializeField] public ProfilePictureView ProfilePictureView { get; private set; }
        [field: SerializeField] public TMP_Text UserNameText { get; private set; }
        [field: SerializeField] public TMP_Text UserAddressText { get; private set; }
        [field: SerializeField] public GameObject VerifiedIcon { get; private set; }
        [field: SerializeField] public GameObject OfficialIcon { get; private set; }
        [field: SerializeField] public CanvasGroup PanelCanvasGroup { get; private set; }

        [field:Header("Toast Animation")]
        [field: SerializeField] public float toastFadeInDuration = 0.3f;
        [field: SerializeField] public float toastVisibleDuration = 1f;
        [field: SerializeField] public float toastVFadeOutDuration = 0.3f;

        [field: Header("Audio")]
        [field: SerializeField] public AudioClipConfig? ShowNotificationSound { get; private set; }

        private readonly ReactiveProperty<ProfileThumbnailViewModel> profileThumbnail = new (ProfileThumbnailViewModel.Default());
        private CancellationTokenSource? loadThumbnailCts;

        private void Start()
        {
            HideToast();
        }

        private void OnDestroy()
        {
            loadThumbnailCts.SafeCancelAndDispose();
        }

        internal void HideToast()
        {
            PanelCanvasGroup.alpha = 0f;
        }

        internal void ConfigureForFriend(Profile.CompactInfo friendProfile, ProfileRepositoryWrapper profileDataProvider)
        {
            Color userColor = friendProfile.UserNameColor;
            UserNameText.color = userColor;
            UserNameText.text = friendProfile.Name;
            UserAddressText.text = $"#{friendProfile.Address.ToString()[^4..]}";
            UserAddressText.gameObject.SetActive(!friendProfile.HasClaimedName);
            VerifiedIcon.SetActive(friendProfile.HasClaimedName);
            OfficialIcon.SetActive(OfficialWalletsHelper.Instance.IsOfficialWallet(friendProfile.Address));

            profileThumbnail.UpdateValue(ProfileThumbnailViewModel.Default(userColor));
            ProfilePictureView.Bind(profileThumbnail);
            loadThumbnailCts = loadThumbnailCts.SafeRestart();
            GetProfileThumbnailCommand.Instance.ExecuteAsync(profileThumbnail, null, friendProfile, loadThumbnailCts.Token).Forget();
        }

        internal async UniTask ShowToastAsync(CancellationToken ct)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(ShowNotificationSound);
            await PanelCanvasGroup.DOFade(1f, toastFadeInDuration).ToUniTask(cancellationToken: ct);
            await UniTask.Delay((int)(toastVisibleDuration * 1000), cancellationToken: ct);
            await PanelCanvasGroup.DOFade(0f, toastVFadeOutDuration).ToUniTask(cancellationToken: ct);
        }
    }
}
