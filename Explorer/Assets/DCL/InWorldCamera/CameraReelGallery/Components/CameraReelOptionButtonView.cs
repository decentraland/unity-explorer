using UnityEngine;
using UnityEngine.UI;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    public class CameraReelOptionButtonView : MonoBehaviour
    {
        [field: SerializeField] internal Button optionButton { get; private set; }

        [field: Header("Context menu settings")]
        [field: SerializeField] internal string publicToggleText { get; private set; }
        [field: Space(10)]
        [field: SerializeField] internal string shareButtonText { get; private set; }
        [field: SerializeField] internal Sprite shareButtonSprite { get; private set; }
        [field: Space(10)]
        [field: SerializeField] internal string copyButtonText { get; private set; }
        [field: SerializeField] internal Sprite copyButtonSprite { get; private set; }
        [field: Space(10)]
        [field: SerializeField] internal string downloadButtonText { get; private set; }
        [field: SerializeField] internal Sprite downloadButtonSprite { get; private set; }
        [field: Space(10)]
        [field: SerializeField] internal string deleteButtonText { get; private set; }
        [field: SerializeField] internal Sprite deleteButtonSprite { get; private set; }
    }
}
