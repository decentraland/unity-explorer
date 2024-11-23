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
        [SerializeField] internal Button userProfileButton;
    }
}
