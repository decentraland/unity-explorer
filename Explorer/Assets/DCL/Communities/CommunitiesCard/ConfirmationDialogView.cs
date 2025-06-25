using Cysharp.Threading.Tasks;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using DG.Tweening;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesCard
{
    public class ConfirmationDialogView : MonoBehaviour
    {
        public struct DialogData
        {
            public struct UserData
            {
                public readonly string Address;
                public readonly string ThumbnailUrl;
                public readonly Color Color;

                public UserData(string address, string thumbnailUrl, Color color)
                {
                    Address = address;
                    ThumbnailUrl = thumbnailUrl;
                    Color = color;
                }
            }

            public readonly string Text;
            public readonly string SubText;
            public readonly string CancelButtonText;
            public readonly string ConfirmButtonText;
            public readonly Sprite Image;
            public readonly bool ShowImageRim;
            public readonly bool ShowQuitImage;
            public readonly UserData UserInfo;

            public DialogData(string text, string cancelButtonText, string confirmButtonText,
                Sprite image, bool showImageRim, bool showQuitImage,
                string subText = "", UserData userInfo = default)
            {
                Text = text;
                CancelButtonText = cancelButtonText;
                ConfirmButtonText = confirmButtonText;
                Image = image;
                ShowImageRim = showImageRim;
                ShowQuitImage = showQuitImage;
                SubText = subText;
                UserInfo = userInfo;
            }
        }

        public enum ConfirmationResult
        {
            CONFIRM,
            CANCEL,
        }

        [field: SerializeField] private CanvasGroup viewCanvasGroup { get; set; }
        [field: SerializeField] private Button backgroundButton { get; set; }
        [field: SerializeField] private Button cancelButton { get; set; }
        [field: SerializeField] private TMP_Text cancelButtonText { get; set; }
        [field: SerializeField] private Button confirmButton { get; set; }
        [field: SerializeField] private TMP_Text confirmButtonText { get; set; }
        [field: SerializeField] private float fadeDuration { get; set; } = 0.3f;
        [field: SerializeField] private TMP_Text mainText { get; set; }
        [field: SerializeField] private TMP_Text subText { get; set; }
        [field: SerializeField] private Image mainImage { get; set; }
        [field: SerializeField] private GameObject quitImage { get; set; }
        [field: SerializeField] private Image rimImage { get; set; }
        [field: SerializeField] private ProfilePictureView profilePictureView { get; set; }
        [field: SerializeField] private Image profileActionIcon { get; set; }

        private readonly UniTask[] closeTasks = new UniTask[3];
        private ProfileRepositoryWrapper profileRepositoryWrapper;

        public void SetProfileRepository(ProfileRepositoryWrapper profileRepositoryWrapper)
        {
            this.profileRepositoryWrapper = profileRepositoryWrapper;
        }

        private UniTask[] GetCloseTasks(CancellationToken ct)
        {
            closeTasks[0] = cancelButton.OnClickAsync(ct);
            closeTasks[1] = backgroundButton.OnClickAsync(ct);
            closeTasks[2] = confirmButton.OnClickAsync(ct);
            return closeTasks;
        }

        public async UniTask<ConfirmationResult> ShowConfirmationDialogAsync(DialogData dialogData,
            CancellationToken ct = default)
        {
            gameObject.SetActive(true);
            cancelButton.gameObject.SetActive(true);
            confirmButton.gameObject.SetActive(true);

            mainText.text = dialogData.Text;
            subText.text = dialogData.SubText;
            subText.gameObject.SetActive(!string.IsNullOrWhiteSpace(dialogData.SubText));
            cancelButtonText.text = dialogData.CancelButtonText;
            confirmButtonText.text = dialogData.ConfirmButtonText;
            rimImage.enabled = dialogData.ShowImageRim;
            quitImage.SetActive(dialogData.ShowQuitImage);
            mainImage.sprite = dialogData.Image;

            bool hasProfileImage = !string.IsNullOrEmpty(dialogData.UserInfo.Address);

            rimImage.gameObject.SetActive(!hasProfileImage);
            profilePictureView.gameObject.SetActive(hasProfileImage);
            profileActionIcon.sprite = dialogData.Image;

            if (hasProfileImage)
            {
                profilePictureView.SetDefaultThumbnail();
                profilePictureView.Setup(profileRepositoryWrapper, dialogData.UserInfo.Color, dialogData.UserInfo.ThumbnailUrl, dialogData.UserInfo.Address);
            }

            await viewCanvasGroup.DOFade(1f, fadeDuration).ToUniTask(cancellationToken: ct);
            viewCanvasGroup.interactable = true;
            viewCanvasGroup.blocksRaycasts = true;

            int index = await UniTask.WhenAny(GetCloseTasks(ct));

            await viewCanvasGroup.DOFade(0f, fadeDuration).ToUniTask(cancellationToken: ct);
            viewCanvasGroup.interactable = false;
            viewCanvasGroup.blocksRaycasts = false;

            cancelButton.gameObject.SetActive(false);
            confirmButton.gameObject.SetActive(false);

            return index > 1 ? ConfirmationResult.CONFIRM : ConfirmationResult.CANCEL;
        }

    }
}
