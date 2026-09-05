using UnityEngine;
using UnityEngine.UI;

namespace DCL.Donations.UI
{
    public class DonationErrorView : MonoBehaviour
    {
        [field: SerializeField] internal Button closeButton { get; set; } = null!;
        [field: SerializeField] internal Button tryAgainButton { get; set; } = null!;
        [field: SerializeField] internal Button contactSupportButton { get; set; } = null!;
    }
}
