using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class PhotoDetailInfoView : MonoBehaviour
    {
        [field: SerializeField] internal TMP_Text dateText { get; private set; }
        [field: SerializeField] internal TMP_Text ownerName { get; private set; }
        [field: SerializeField] internal TMP_Text sceneInfo { get; private set; }
        [field: SerializeField] internal Button jumpInButton { get; private set; }
        [field: SerializeField] internal Button ownerProfileButton { get; private set; }
        [field: SerializeField] internal GameObject unusedEquippedWearableViewContainer { get; private set; }
        [field: SerializeField] internal RectTransform visiblePersonContainer { get; private set; }
        [field: SerializeField] internal ScrollRect visiblePersonScrollRect { get; private set; }
        [field: SerializeField] internal InfoSidePanelLoadingView loadingState { get; private set; }

        [field: Header("Prefabs")]
        [field: SerializeField] internal VisiblePersonView visiblePersonViewPrefab { get; private set; }
        [field: SerializeField] internal EquippedWearableView equippedWearablePrefab { get; private set; }
    }
}
