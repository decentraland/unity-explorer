using TMPro;
using UnityEngine;

namespace DCL.UI.PortableExperiences.SummaryPopup
{
    public class GlobalPxEntryView : MonoBehaviour
    {
        [field: SerializeField]
        internal TMP_Text pxName = null!;

        public void Configure(string pxDisplayName) => pxName.text = pxDisplayName;
    }
}
