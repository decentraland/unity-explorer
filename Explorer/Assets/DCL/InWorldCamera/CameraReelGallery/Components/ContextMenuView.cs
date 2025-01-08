using UnityEngine;
using UnityEngine.UI;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    public class ContextMenuView : MonoBehaviour
    {
        [field: SerializeField] internal Button backgroundCloseButton { get; private set; }
        [field: SerializeField] internal GameObject controlsParent { get; private set; }

        [field: Header("Controls")]
        [field: SerializeField] internal Toggle setAsPublic { get; private set; }
        [field: SerializeField] internal Button shareOnX { get; private set; }
        [field: SerializeField] internal Button copyLink { get; private set; }
        [field: SerializeField] internal Button download { get; private set; }
        [field: SerializeField] internal Button delete { get; private set; }

        [field: Header("Configuration")]
        [field: SerializeField] internal Vector3 offsetFromTarget { get; private set; } = Vector3.zero;
    }
}
