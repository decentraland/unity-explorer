using DCL.AvatarRendering.Wearables.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.PortableExperiences.SummaryPopup
{
    public class SmartWearableEntryView : MonoBehaviour
    {
        [field: SerializeField]
        internal TMP_Text pxName = null!;

        [field: SerializeField]
        internal Button removeButton = null!;

        public void Configure(IWearable wearable)
        {
            pxName.text = wearable.GetName();
        }
    }
}
