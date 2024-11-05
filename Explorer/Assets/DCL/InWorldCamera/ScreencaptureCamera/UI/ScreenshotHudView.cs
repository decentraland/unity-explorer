using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using UnityEngine;

namespace DCL.InWorldCamera.ScreencaptureCamera.UI
{
    public class ScreenshotHudView : MonoBehaviour, ICoroutineRunner
    {
        public Texture2D Screenshot;
        public ScreenshotMetadata Metadata;
    }
}
