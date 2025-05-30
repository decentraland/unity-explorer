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

        [field: SerializeField] public CanvasGroup ViewCanvasGroup { get; private set; }
        [field: SerializeField] public Button BackgroundButton { get; private set; }
        [field: SerializeField] public Button CancelButton { get; private set; }
        [field: SerializeField] public TMP_Text CancelButtonText { get; private set; }
        [field: SerializeField] public Button ConfirmButton { get; private set; }
        [field: SerializeField] public TMP_Text ConfirmButtonText { get; private set; }
        [field: SerializeField] public float FadeDuration { get; private set; } = 0.3f;
        [field: SerializeField] public TMP_Text MainText { get; private set; }
        [field: SerializeField] public Image MainImage { get; private set; }
        [field: SerializeField] public GameObject QuitImage { get; private set; }
        [field: SerializeField] public Image RimImage { get; private set; }

        public async UniTask<ConfirmationResult> ShowConfirmationDialogAsync(DialogData dialogData,
            CancellationToken ct = default)
        {
            gameObject.SetActive(true);
            CancelButton.gameObject.SetActive(true);
            ConfirmButton.gameObject.SetActive(true);

            MainText.text = dialogData.Text;
            CancelButtonText.text = dialogData.CancelButtonText;
            ConfirmButtonText.text = dialogData.ConfirmButtonText;
            RimImage.enabled = dialogData.ShowImageRim;
            QuitImage.SetActive(dialogData.ShowQuitImage);
            MainImage.sprite = dialogData.Image;

            await ViewCanvasGroup.DOFade(1f, FadeDuration).ToUniTask(cancellationToken: ct);
            ViewCanvasGroup.interactable = true;
            ViewCanvasGroup.blocksRaycasts = true;

            int index = await UniTask.WhenAny(CancelButton.OnClickAsync(ct), BackgroundButton.OnClickAsync(ct), ConfirmButton.OnClickAsync(ct));

            await ViewCanvasGroup.DOFade(0f, FadeDuration).ToUniTask(cancellationToken: ct);
            ViewCanvasGroup.interactable = false;
            ViewCanvasGroup.blocksRaycasts = false;

            CancelButton.gameObject.SetActive(false);
            ConfirmButton.gameObject.SetActive(false);

            return index > 1 ? ConfirmationResult.CONFIRM : ConfirmationResult.CANCEL;
        }

    }
}
