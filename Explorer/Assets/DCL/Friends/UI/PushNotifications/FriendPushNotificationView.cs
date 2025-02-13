using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Chat;
using DCL.UI;
using DG.Tweening;
using MVC;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Friends.UI.PushNotifications
{
    public class FriendPushNotificationView : ViewBase, IView
    {
        [field: SerializeField] public Image ThumbnailBackground { get; private set; }
        [field: SerializeField] public ImageView ThumbnailImageView { get; private set; }
        [field: SerializeField] public TMP_Text UserNameText { get; private set; }
        [field: SerializeField] public TMP_Text UserAddressText { get; private set; }
        [field: SerializeField] public CanvasGroup PanelCanvasGroup { get; private set; }
        [field: SerializeField] public ChatEntryConfigurationSO ChatEntryConfiguration { get; private set; }

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

        internal void ConfigureForFriend(FriendProfile friendProfile, Sprite? profileThumbnail)
        {
            Color userColor = ChatEntryConfiguration.GetNameColor(friendProfile.Name);
            ThumbnailBackground.color = userColor;
            UserNameText.color = userColor;

            UserNameText.text = friendProfile.Name;
            UserAddressText.text = $"#{friendProfile.Address.ToString()[^4..]}";
            UserAddressText.gameObject.SetActive(friendProfile.HasClaimedName);

            if (profileThumbnail != null)
                ThumbnailImageView.SetImage(profileThumbnail);
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
