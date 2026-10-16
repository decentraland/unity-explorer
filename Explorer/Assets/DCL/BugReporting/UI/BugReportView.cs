using MVC;
using System;
using System.Collections.Generic;
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

    /// <summary>One attached screenshot in the form; the slots fill in attachment order.</summary>
    [Serializable]
    public struct BugReportScreenshotSlot
    {
        public RawImage Preview;
        public Button RemoveButton;
    }

    public class BugReportView : ViewBase, IView
    {
        private const float DISABLED_LABEL_ALPHA = 0.5f;

        [field: Header("Form")]
        [field: SerializeField] public TMP_Dropdown IssueTypeDropdown { get; private set; } = null!;
        [field: SerializeField] public TMP_InputField DescriptionInput { get; private set; } = null!;
        [field: SerializeField] public TMP_Text DescriptionCharCounter { get; private set; } = null!;
        [field: SerializeField] public Toggle ShareLogsToggle { get; private set; } = null!;
        [field: SerializeField] public Button SubmitButton { get; private set; } = null!;
        [field: SerializeField] public TMP_Text SubmitButtonLabel { get; private set; } = null!;
        [field: SerializeField] public Button CancelButton { get; private set; } = null!;
        [field: SerializeField] public Button CloseButton { get; private set; } = null!;

        [field: Header("Screenshots")]
        [field: SerializeField] public GameObject ScreenshotSection { get; private set; } = null!;
        [field: SerializeField] public Button AttachScreenshotButton { get; private set; } = null!;
        [field: SerializeField] public BugReportScreenshotSlot[] ScreenshotSlots { get; private set; } = null!;

        [field: Header("States")]
        [field: SerializeField] public GameObject FormPanel { get; private set; } = null!;
        [field: SerializeField] public GameObject SuccessPanel { get; private set; } = null!;
        [field: SerializeField] public Button SuccessDoneButton { get; private set; } = null!;

        /// <summary>Called by the controller once, right after the view is instantiated.</summary>
        public void Initialize()
        {
            WireCharCounter(DescriptionInput, DescriptionCharCounter);

            // Each preview hangs from the left edge of its slot; SetScreenshots sizes it to the texture's aspect ratio.
            foreach (BugReportScreenshotSlot slot in ScreenshotSlots)
            {
                RectTransform previewRect = slot.Preview.rectTransform;
                previewRect.anchorMin = previewRect.anchorMax = previewRect.pivot = new Vector2(0f, 0.5f);
                previewRect.anchoredPosition = Vector2.zero;
            }
        }

        public void ShowState(BugReportViewState state)
        {
            FormPanel.SetActive(state == BugReportViewState.Form);
            SuccessPanel.SetActive(state == BugReportViewState.Success);
        }

        /// <summary>The button's color tint only fades its background, so the label is faded here to match.</summary>
        public void SetSubmitInteractable(bool interactable)
        {
            SubmitButton.interactable = interactable;

            Color labelColor = SubmitButtonLabel.color;
            labelColor.a = interactable ? 1f : DISABLED_LABEL_ALPHA;
            SubmitButtonLabel.color = labelColor;
        }

        /// <summary>The field can still be focused when the view closes, so a reopen hides the counter explicitly.</summary>
        public void HideCharCounter() =>
            DescriptionCharCounter.gameObject.SetActive(false);

        /// <summary>Fills the slots in order with the given images and hides the rest.</summary>
        public void SetScreenshots(IReadOnlyList<BugReportImage> images, bool canAttachMore)
        {
            for (var i = 0; i < ScreenshotSlots.Length; i++)
            {
                BugReportScreenshotSlot slot = ScreenshotSlots[i];
                Texture2D? texture = i < images.Count ? images[i].Preview : null;

                slot.Preview.texture = texture;

                if (texture != null)
                    FitPreviewToSlot(slot.Preview, texture);

                slot.Preview.gameObject.SetActive(texture != null);
                slot.RemoveButton.gameObject.SetActive(texture != null);
            }

            AttachScreenshotButton.gameObject.SetActive(canAttachMore);
        }

        /// <summary>Sizes the preview to the largest rect at the texture's aspect ratio that fits its slot.</summary>
        private static void FitPreviewToSlot(RawImage preview, Texture2D texture)
        {
            Rect slot = ((RectTransform)preview.rectTransform.parent).rect;
            float aspect = texture.width / (float)texture.height;
            float height = Mathf.Min(slot.height, slot.width / aspect);
            preview.rectTransform.sizeDelta = new Vector2(height * aspect, height);
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
