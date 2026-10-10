using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    public class MonthGridView : MonoBehaviour
    {
        [field: SerializeField] internal TMP_Text monthText { get; private set; }
        [field: SerializeField] internal GridLayoutGroup gridLayoutGroup { get; private set; }
    }
}
