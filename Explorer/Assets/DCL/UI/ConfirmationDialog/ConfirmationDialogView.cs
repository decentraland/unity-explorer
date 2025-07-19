using Cysharp.Threading.Tasks;
using DCL.UI.ConfirmationDialog.Opener;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using MVC;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.ConfirmationDialog
{
    public class ConfirmationDialogView : ViewBase, IView
    {
        [field: SerializeField] private Button backgroundButton { get; set; } = null!;
        [field: SerializeField] private ButtonWithAnimationView cancelButton { get; set; } = null!;
        [field: SerializeField] private TMP_Text cancelButtonText { get; set; } = null!;
        [field: SerializeField] private ButtonWithAnimationView confirmButton { get; set; } = null!;
        [field: SerializeField] private TMP_Text confirmButtonText { get; set; } = null!;
        [field: SerializeField] private TMP_Text mainText { get; set; } = null!;
        [field: SerializeField] private TMP_Text subText { get; set; } = null!;
        [field: SerializeField] private Image mainImage { get; set; } = null!;
        [field: SerializeField] private GameObject quitImage { get; set; } = null!;
        [field: SerializeField] private Image rimImage { get; set; } = null!;
        [field: SerializeField] private ProfilePictureView profilePictureView { get; set; } = null!;
        [field: SerializeField] private Image profileActionIcon { get; set; } = null!;

        private readonly UniTask[] closeTasks = new UniTask[3];

        public UniTask[] GetCloseTasks(CancellationToken ct)
        {
            closeTasks[0] = cancelButton.Button.OnClickAsync(ct);
            closeTasks[1] = backgroundButton.OnClickAsync(ct);
            closeTasks[2] = confirmButton.Button.OnClickAsync(ct);
            return closeTasks;
        }

        public void Configure(ConfirmationDialogParameter dialogData, ProfileRepositoryWrapper profileRepositoryWrapper)
        {
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

            if (!hasProfileImage) return;

            profilePictureView.SetDefaultThumbnail();
            profilePictureView.Setup(profileRepositoryWrapper, dialogData.UserInfo.Color, dialogData.UserInfo.ThumbnailUrl);
        }

        public void Reset()
        {
            //Reset scale to revert pressed state
            cancelButton.ResetButtonAnimationScale();
            confirmButton.ResetButtonAnimationScale();

            cancelButton.gameObject.SetActive(false);
            confirmButton.gameObject.SetActive(false);
        }

    }
}
