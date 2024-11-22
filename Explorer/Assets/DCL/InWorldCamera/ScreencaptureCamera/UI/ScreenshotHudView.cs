using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using UnityEngine;

namespace DCL.InWorldCamera.ScreencaptureCamera.UI
{
    // TODO (Vit): This is a a placeholder UI that will be replaced by a proper UI in near future
    public class ScreenshotHudView : MonoBehaviour
    {
        public Canvas Canvas;

        [Space]
        public Texture2D Screenshot;
        public ScreenshotMetadata Metadata;
    }
}
