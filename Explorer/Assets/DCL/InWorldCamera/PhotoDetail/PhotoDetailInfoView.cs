using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class PhotoDetailInfoView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text dateText;
        [SerializeField] internal TMP_Text ownerName;
        [SerializeField] internal TMP_Text sceneInfo;
        [SerializeField] internal Button jumpInButton;
        [SerializeField] internal Button ownerProfileButton;
        [SerializeField] internal GameObject unusedVisiblePersonViewContainer;
        [SerializeField] internal RectTransform visiblePersonContainer;
        [SerializeField] internal GameObject loadingState;

        [Header("Prefabs")]
        [SerializeField] internal VisiblePersonView visiblePersonViewPrefab;
    }
}
