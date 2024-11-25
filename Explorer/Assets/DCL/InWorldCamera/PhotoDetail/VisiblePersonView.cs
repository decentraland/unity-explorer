using DCL.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class VisiblePersonView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text userName;
        [SerializeField] internal ImageView profileImage;
        [SerializeField] internal Button expandWearableButton;
        [SerializeField] internal RectTransform expandWearableButtonImage;
        [SerializeField] internal Button userProfileButton;
        [SerializeField] internal RectTransform wearableListContainer;
        [SerializeField] internal float expandAnimationDuration = 0.3f;
        [SerializeField] internal GameObject wearableListLoadingSpinner;
        [SerializeField] internal GameObject wearableListEmptyMessage;
    }
}
