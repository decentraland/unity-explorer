using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.InWorldCamera.ScreencaptureCamera.UI
{
    public class InWorldCameraView : ViewBase, IView
    {
        [field: Header("BUTTONS")]
        [field: SerializeField] public Button CameraReelButton { get; private set; }
        [field: SerializeField] public Button TakeScreenshotButton { get; private set; }
        [field: SerializeField] public Button CloseButton { get; private set; }
        [field: SerializeField] public Button ShortcutsInfoButton { get; private set; }

        [Header("SHORTCUTS INFO PANEL")]
        [SerializeField] private GameObject shortcutsInfoPanel;
        [SerializeField] private Image openShortcutsIcon;
        [SerializeField] private Image closeShortcutsIcon;
    }
}
