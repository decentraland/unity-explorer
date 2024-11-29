using UnityEngine;
using UnityEngine.UI;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    public class ContextMenuView : MonoBehaviour
    {
        [SerializeField] internal Button backgroundCloseButton;
        [SerializeField] internal GameObject controlsParent;

        [Header("Controls")]
        [SerializeField] internal Toggle setAsPublic;
        [SerializeField] internal Button shareOnX;
        [SerializeField] internal Button copyLink;
        [SerializeField] internal Button download;
        [SerializeField] internal Button delete;

        [Header("Configuration")]
        [SerializeField] internal Vector3 offsetFromTarget = Vector3.zero;
    }
}
