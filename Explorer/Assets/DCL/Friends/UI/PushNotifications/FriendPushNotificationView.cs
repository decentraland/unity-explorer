using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.UI.Profiles.Helpers;
using DCL.UI.ProfileElements;
using DG.Tweening;
using MVC;
using System.Threading;
using TMPro;
using UnityEngine;

namespace DCL.Friends.UI.PushNotifications
{
    public class FriendPushNotificationView : ViewBase, IView
    {
        [field: SerializeField] public ProfilePictureView ProfilePictureView { get; private set; }
        [field: SerializeField] public TMP_Text UserNameText { get; private set; }
        [field: SerializeField] public TMP_Text UserAddressText { get; private set; }
        [field: SerializeField] public GameObject VerifiedIcon { get; private set; }
        [field: SerializeField] public CanvasGroup PanelCanvasGroup { get; private set; }

        [field:Header("Toast Animation")]
        [field: SerializeField] public float toastFadeInDuration = 0.3f;
        [field: SerializeField] public float toastVisibleDuration = 1f;
        [field: SerializeField] public float toastVFadeOutDuration = 0.3f;

        [field: Header("Audio")]
        [field: SerializeField] public AudioClipConfig? ShowNotificationSound { get; private set; }

        private void Start()
        {
            HideToast();
        }

        internal void HideToast()
        {
            PanelCanvasGroup.alpha = 0f;
        }

        internal void ConfigureForFriend(FriendProfile friendProfile)
        {
            Color userColor = friendProfile.UserNameColor;
            UserNameText.color = userColor;
            UserNameText.text = friendProfile.Name;
            UserAddressText.text = $"#{friendProfile.Address.ToString()[^4..]}";
            UserAddressText.gameObject.SetActive(!friendProfile.HasClaimedName);
            VerifiedIcon.SetActive(friendProfile.HasClaimedName);
            ProfilePictureView.Setup(friendProfile.UserNameColor, friendProfile.FacePictureUrl, friendProfile.Address);
        }

        internal async UniTask ShowToastAsync(CancellationToken ct)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(ShowNotificationSound);
            await PanelCanvasGroup.DOFade(1f, toastFadeInDuration).ToUniTask(cancellationToken: ct);
            await UniTask.Delay((int)(toastVisibleDuration * 1000), cancellationToken: ct);
            await PanelCanvasGroup.DOFade(0f, toastVFadeOutDuration).ToUniTask(cancellationToken: ct);
        }

        public void SetProfileDataProvider(ProfileRepositoryWrapper profileDataProvider)
        {
            ProfilePictureView.SetProfileDataProvider(profileDataProvider);
        }
    }
}
