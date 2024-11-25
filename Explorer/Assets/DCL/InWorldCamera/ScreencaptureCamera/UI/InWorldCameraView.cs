using MVC;
using UnityEngine;

namespace DCL.InWorldCamera.ScreencaptureCamera.UI
{
    public class InWorldCameraView : ViewBase, IView
    {
        // [field: SerializeField] public GameObject Hud { get; private set; } => this.GameObject;
        public GameObject Hud => this.gameObject;
    }
}
