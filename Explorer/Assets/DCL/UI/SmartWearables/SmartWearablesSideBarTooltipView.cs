using MVC;
using TMPro;
using UnityEngine;
using Utility;

namespace DCL.UI.Skybox
{
    public class SmartWearablesSideBarTooltipView : ViewBase, IView
    {
        [field: Header("Smart Wearables Allowed")]
        [field: SerializeField]
        public GameObject SmartWearablesAllowedContent { get; private set; } = null!;

        [field: SerializeField]
        private TMP_Text EquippedCountText;

        [field: SerializeField]
        [field: TextArea]
        public string FormatActiveCountOnly { get; private set; } = null!;

        [field: SerializeField]
        [field: TextArea]
        public string FormatInactiveCountOnly { get; private set; } = null!;

        [field: SerializeField]
        [field: TextArea]
        public string FormatBothActiveAndInactiveCount { get; private set; } = null!;

        [field: Header("Smart Wearables Banned")]
        [field: SerializeField]
        public GameObject SmartWearablesBannedContent { get; private set; } = null!;

        public void Setup(bool smartWearablesAllowed, int equippedCount, int killedCount)
        {
            SmartWearablesAllowedContent.SetActive(smartWearablesAllowed);

            if (smartWearablesAllowed)
            {
                if (killedCount == 0)
                    EquippedCountText.text = FormatActiveCountOnly.Pluralize("count", equippedCount);
                else if (equippedCount == 0)
                    EquippedCountText.text = FormatInactiveCountOnly.Pluralize("count", killedCount);
                else
                    EquippedCountText.text = FormatBothActiveAndInactiveCount.Pluralize("active-count", equippedCount)
                                                                             .Pluralize("inactive-count", killedCount);
            }

            SmartWearablesBannedContent.SetActive(!smartWearablesAllowed);
        }
    }
}
