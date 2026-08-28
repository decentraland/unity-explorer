using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.BugReporting.UI
{
    public enum BugReportViewState
    {
        Form,
        Success,
    }

    public class BugReportView : ViewBase, IView
    {
        [field: Header("Form")]
        [field: SerializeField] public TMP_Dropdown IssueTypeDropdown { get; private set; } = null!;
        [field: SerializeField] public TMP_InputField DescriptionInput { get; private set; } = null!;
        [field: SerializeField] public TMP_Text DescriptionCharCounter { get; private set; } = null!;
        [field: SerializeField] public Toggle ShareLogsToggle { get; private set; } = null!;
        [field: SerializeField] public Button SubmitButton { get; private set; } = null!;
        [field: SerializeField] public Button CancelButton { get; private set; } = null!;
        [field: SerializeField] public Button CloseButton { get; private set; } = null!;

        [field: Header("Screenshot")]
        [field: SerializeField] public GameObject ScreenshotSection { get; private set; } = null!;
        [field: SerializeField] public RawImage ScreenshotPreview { get; private set; } = null!;
        [field: SerializeField] public Button AttachScreenshotButton { get; private set; } = null!;
        [field: SerializeField] public Button RemoveScreenshotButton { get; private set; } = null!;

        [field: Header("States")]
        [field: SerializeField] public GameObject FormPanel { get; private set; } = null!;
        [field: SerializeField] public GameObject SuccessPanel { get; private set; } = null!;
        [field: SerializeField] public Button SuccessDoneButton { get; private set; } = null!;

        /// <summary>Called by the controller once, right after the view is instantiated.</summary>
        public void Initialize()
        {
            WireCharCounter(DescriptionInput, DescriptionCharCounter);

            // The preview hangs from the left edge of its slot; SetScreenshot sizes it to the texture's aspect ratio.
            RectTransform previewRect = ScreenshotPreview.rectTransform;
            previewRect.anchorMin = previewRect.anchorMax = previewRect.pivot = new Vector2(0f, 0.5f);
            previewRect.anchoredPosition = Vector2.zero;
        }

        public void ShowState(BugReportViewState state)
        {
            FormPanel.SetActive(state == BugReportViewState.Form);
            SuccessPanel.SetActive(state == BugReportViewState.Success);
        }

        /// <summary>The field can still be focused when the view closes, so a reopen hides the counter explicitly.</summary>
        public void HideCharCounter() =>
            DescriptionCharCounter.gameObject.SetActive(false);

        public void SetScreenshot(Texture2D? texture)
        {
            ScreenshotPreview.texture = texture;

            if (texture != null)
                FitScreenshotPreviewToSlot(texture);

            ScreenshotPreview.gameObject.SetActive(texture != null);
            RemoveScreenshotButton.gameObject.SetActive(texture != null);
            AttachScreenshotButton.gameObject.SetActive(texture == null);
        }

        /// <summary>Sizes the preview to the largest rect at the texture's aspect ratio that fits its slot.</summary>
        private void FitScreenshotPreviewToSlot(Texture2D texture)
        {
            Rect slot = ((RectTransform)ScreenshotPreview.rectTransform.parent).rect;
            float aspect = texture.width / (float)texture.height;
            float height = Mathf.Min(slot.height, slot.width / aspect);
            ScreenshotPreview.rectTransform.sizeDelta = new Vector2(height * aspect, height);
        }

        // Refreshes on focus as well as on typing: the controller fills the field with SetTextWithoutNotify, which skips onValueChanged.
        private static void WireCharCounter(TMP_InputField input, TMP_Text counter)
        {
            input.onValueChanged.AddListener(_ => RefreshCharCounter(input, counter));

            input.onSelect.AddListener(_ =>
            {
                RefreshCharCounter(input, counter);
                counter.gameObject.SetActive(true);
            });

            input.onDeselect.AddListener(_ => counter.gameObject.SetActive(false));
            counter.gameObject.SetActive(false);
        }

        private static void RefreshCharCounter(TMP_InputField input, TMP_Text counter) =>
            counter.text = $"{input.text.Length}/{input.characterLimit}";
    }
}
