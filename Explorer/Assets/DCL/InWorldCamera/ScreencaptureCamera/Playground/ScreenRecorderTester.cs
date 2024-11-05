using DCL.InWorldCamera.ScreencaptureCamera.UI;
using UnityEngine;

namespace DCL.InWorldCamera.ScreencaptureCamera.Playground
{
    public class ScreenRecorderTester : MonoBehaviour
    {
        private ScreenRecorder recorder;
        public RectTransform canvasRectTransform;
        public Texture2D Texture;

        public ScreenshotHudView hud;

        [ContextMenu(nameof(SHOOT))]
        public void SHOOT()
        {
            recorder ??= new ScreenRecorder(canvasRectTransform);
            hud.StartCoroutine(recorder.CaptureScreenshot(Show));
        }

        private void Show(Texture2D texture)
        {
            Texture = texture;
        }
    }
}
