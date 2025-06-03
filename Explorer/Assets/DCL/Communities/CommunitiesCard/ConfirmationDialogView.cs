using Cysharp.Threading.Tasks;
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
            public readonly string Text;
            public readonly string CancelButtonText;
            public readonly string ConfirmButtonText;
            public readonly Sprite Image;
            public readonly bool ShowImageRim;
            public readonly bool ShowQuitImage;

            public DialogData(string text, string cancelButtonText, string confirmButtonText, Sprite image, bool showImageRim, bool showQuitImage)
            {
                Text = text;
                CancelButtonText = cancelButtonText;
                ConfirmButtonText = confirmButtonText;
                Image = image;
                ShowImageRim = showImageRim;
                ShowQuitImage = showQuitImage;
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
        [field: SerializeField] private Image mainImage { get; set; }
        [field: SerializeField] private GameObject quitImage { get; set; }
        [field: SerializeField] private Image rimImage { get; set; }

        public async UniTask<ConfirmationResult> ShowConfirmationDialogAsync(DialogData dialogData,
            CancellationToken ct = default)
        {
            gameObject.SetActive(true);
            cancelButton.gameObject.SetActive(true);
            confirmButton.gameObject.SetActive(true);

            mainText.text = dialogData.Text;
            cancelButtonText.text = dialogData.CancelButtonText;
            confirmButtonText.text = dialogData.ConfirmButtonText;
            rimImage.enabled = dialogData.ShowImageRim;
            quitImage.SetActive(dialogData.ShowQuitImage);
            mainImage.sprite = dialogData.Image;

            await viewCanvasGroup.DOFade(1f, fadeDuration).ToUniTask(cancellationToken: ct);
            viewCanvasGroup.interactable = true;
            viewCanvasGroup.blocksRaycasts = true;

            int index = await UniTask.WhenAny(cancelButton.OnClickAsync(ct), backgroundButton.OnClickAsync(ct), confirmButton.OnClickAsync(ct));

            await viewCanvasGroup.DOFade(0f, fadeDuration).ToUniTask(cancellationToken: ct);
            viewCanvasGroup.interactable = false;
            viewCanvasGroup.blocksRaycasts = false;

            cancelButton.gameObject.SetActive(false);
            confirmButton.gameObject.SetActive(false);

            return index > 1 ? ConfirmationResult.CONFIRM : ConfirmationResult.CANCEL;
        }

    }
}
