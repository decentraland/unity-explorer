using DCL.UI.GenericContextMenu.Controls.Configs;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    public class CameraReelOptionButtonView : MonoBehaviour
    {
        [field: SerializeField] internal Button optionButton { get; private set; }

        [field: Header("Context menu settings")]
        [field: SerializeField] internal ContextMenuControlSettings publicControl { get; private set; }
        [field: SerializeField] internal ContextMenuControlSettings shareControl { get; private set; }
        [field: SerializeField] internal ContextMenuControlSettings copyControl { get; private set; }
        [field: SerializeField] internal ContextMenuControlSettings downloadControl { get; private set; }
        [field: SerializeField] internal ContextMenuControlSettings deleteControl { get; private set; }
    }
}
