using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class EquippedWearableView : MonoBehaviour
    {
        [field: SerializeField] internal Image wearableIcon { get; private set; }
        [field: SerializeField] internal TMP_Text wearableName { get; private set; }
        [field: SerializeField] internal Button wearableBuyButton { get; private set; }
        [field: SerializeField] internal float buyButtonAnimationDuration { get; private set; } = 0.3f;
    }
}
