using MVC;
using TMPro;
using UnityEngine;

namespace DCL.UI.Skybox
{
    public class SmartWearablesSideBarTooltipView : ViewBase, IView
    {
        [field: Header("Smart Wearables Allowed")]
        [field: SerializeField]
        public GameObject SmartWearablesAllowedContent { get; private set; }

        [field: SerializeField]
        private TMP_Text EquippedCountText;

        [field: SerializeField]
        [field: TextArea]
        public string EquippedCountFormat { get; private set; }

        [field: Header("Smart Wearables Banned")]
        [field: SerializeField]
        public GameObject SmartWearablesBannedContent { get; private set; }

        public void Setup(bool smartWearablesAllowed, int equippedCount)
        {
            SmartWearablesAllowedContent.SetActive(smartWearablesAllowed);
            if (smartWearablesAllowed) EquippedCountText.text = string.Format(EquippedCountFormat, equippedCount);

            SmartWearablesBannedContent.SetActive(!smartWearablesAllowed);
        }
    }
}
