using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using UnityEngine;

namespace DCL.InWorldCamera.ScreencaptureCamera.UI
{
    public class ScreenshotHudView : MonoBehaviour, ICoroutineRunner
    {
        public Canvas Canvas;

        [Space]
        public Texture2D Screenshot;
        public ScreenshotMetadata Metadata;

        public void AssignScreenshot(Texture2D screenshot)
        {
            Screenshot = screenshot;
            Canvas.enabled = true;
        }
    }
}
