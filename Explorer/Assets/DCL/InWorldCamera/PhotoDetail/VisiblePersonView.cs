using DCL.UI;
using DCL.UI.ProfileElements;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class VisiblePersonView : MonoBehaviour
    {
        [field: SerializeField] internal TMP_Text userName { get; private set; }
        [field: SerializeField] internal TMP_Text userNameTag { get; private set; }
        [field: SerializeField] internal ProfilePictureView profilePictureView { get; private set; }
        [field: SerializeField] internal Button expandWearableButton { get; private set; }
        [field: SerializeField] internal RectTransform expandWearableButtonImage { get; private set; }
        [field: SerializeField] internal Button userProfileButton { get; private set; }
        [field: SerializeField] internal RectTransform wearableListContainer { get; private set; }
        [field: SerializeField] internal float expandAnimationDuration { get; private set; } = 0.3f;
        [field: SerializeField] internal GameObject wearableListLoadingSpinner { get; private set; }
        [field: SerializeField] internal GameObject wearableListEmptyMessage { get; private set; }
    }
}
