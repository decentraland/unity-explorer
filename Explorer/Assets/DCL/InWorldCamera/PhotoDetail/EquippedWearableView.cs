using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class EquippedWearableView : MonoBehaviour
    {
        [SerializeField] internal Image wearableIcon;
        [SerializeField] internal TMP_Text wearableName;
        [SerializeField] internal Button wearableBuyButton;
        [SerializeField] internal float buyButtonAnimationDuration = 0.3f;
    }
}
