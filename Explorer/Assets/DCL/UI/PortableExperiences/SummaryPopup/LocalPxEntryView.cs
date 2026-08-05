using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.PortableExperiences.SummaryPopup
{
    public class LocalPxEntryView : MonoBehaviour
    {
        [field: SerializeField]
        internal TMP_Text pxName = null!;

        [field: SerializeField]
        internal Button removeButton = null!;

        public void Configure(string pxDisplayName) => pxName.text = pxDisplayName;
    }
}
