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
        [field: SerializeField] internal TMP_Dropdown IssueTypeDropdown { get; private set; } = null!;
        [field: SerializeField] internal TMP_InputField DescriptionInput { get; private set; } = null!;
        [field: SerializeField] internal Toggle ShareLogsToggle { get; private set; } = null!;
        [field: SerializeField] internal Button SubmitButton { get; private set; } = null!;
        [field: SerializeField] internal Button CancelButton { get; private set; } = null!;
        [field: SerializeField] internal Button CloseButton { get; private set; } = null!;

        [field: Header("Screenshot")]
        [field: SerializeField] internal GameObject ScreenshotSection { get; private set; } = null!;
        [field: SerializeField] internal RawImage ScreenshotPreview { get; private set; } = null!;
        [field: SerializeField] internal Button AttachScreenshotButton { get; private set; } = null!;
        [field: SerializeField] internal Button RemoveScreenshotButton { get; private set; } = null!;

        [field: Header("States")]
        [field: SerializeField] internal GameObject FormPanel { get; private set; } = null!;
        [field: SerializeField] internal GameObject SuccessPanel { get; private set; } = null!;
        [field: SerializeField] internal Button SuccessDoneButton { get; private set; } = null!;

        public void ShowState(BugReportViewState state)
        {
            FormPanel.SetActive(state == BugReportViewState.Form);
            SuccessPanel.SetActive(state == BugReportViewState.Success);
        }

        public void SetScreenshot(Texture2D? texture)
        {
            ScreenshotPreview.texture = texture;
            ScreenshotPreview.gameObject.SetActive(texture != null);
            RemoveScreenshotButton.gameObject.SetActive(texture != null);
            AttachScreenshotButton.gameObject.SetActive(texture == null);
        }
    }
}
