using DCL.InWorldCamera.ReelActions;
using DCL.UI;
using TMPro;
using UnityEngine;
using Utility;

namespace DCL.InWorldCamera.CameraReelToast
{
    public sealed class DownloadNotificationView : MonoBehaviour
    {
        [SerializeField] private WarningNotificationView notificationView;
        [SerializeField] private TMP_Text fileLocationText;

        private bool wasClicked;

        public WarningNotificationView NotificationView => notificationView;

        internal void OnShow()
        {
            fileLocationText.text = ReelCommonActions.ReelsPath;
            wasClicked = false;
        }

        public void OpenFileLocation()
        {
            if (wasClicked)
                return;

            PlatformUtils.ShellExecute(ReelCommonActions.ReelsPath);
            wasClicked = true;
        }
    }
}
