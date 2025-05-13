using DCL.InWorldCamera.ReelActions;
using DCL.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.InWorldCamera.CameraReelToast
{
    public sealed class DownloadNotificationView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text fileLocationText;
        [SerializeField] private WarningNotificationView notificationView;

        private bool wasClicked;

        private void Start()
        {
            button.onClick.AddListener(OpenFileLocation);
            fileLocationText.text = ReelCommonActions.ReelsPath;
        }

        public WarningNotificationView NotificationView => notificationView;

        public void PrepareToBeClicked() =>
            wasClicked = false;

        private void OpenFileLocation()
        {
            if (wasClicked)
                return;

            PlatformUtils.ShellExecute(ReelCommonActions.ReelsPath);
            wasClicked = true;
        }
    }
}
