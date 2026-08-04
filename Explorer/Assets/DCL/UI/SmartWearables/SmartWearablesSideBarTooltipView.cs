using MVC;
using System;
using TMPro;
using UnityEngine;

namespace DCL.UI.Skybox
{
    public class SmartWearablesSideBarTooltipView : ViewBase, IView
    {
        [Serializable]
        public struct PXSummary
        {
            public TMP_Text AllowedText;
            public TMP_Text RunningText;
            public TMP_Text KilledText;
        }

        [field: SerializeField]
        private TMP_Text titleText = null!;

        [field: SerializeField]
        private TMP_Text globalPxRunningText = null!;

        [field: SerializeField]
        private PXSummary smartWearablePxSummary;

        [field: SerializeField]
        private PXSummary localPxSummary;

        [field: SerializeField]
        [field: TextArea]
        public string FormatAllowed { get; private set; } = null!;

        [field: SerializeField]
        [field: TextArea]
        public string FormatRunning { get; private set; } = null!;

        [field: SerializeField]
        [field: TextArea]
        public string FormatKilled { get; private set; } = null!;

        [field: SerializeField]
        [field: TextArea]
        public string FormatTitle { get; private set; } = null!;

        public void Setup(int allowedSmartWearableCount, int equippedSmartWearableCount, int killedSmartWearableCount,
            int runningGlobalPortableExperienceCount,
            int allowedLocalPortableExperienceCount, int runningLocalPortableExperienceCount, int killedLocalPortableExperienceCount)
        {
            smartWearablePxSummary.AllowedText.text = string.Format(FormatAllowed, allowedSmartWearableCount);
            smartWearablePxSummary.RunningText.text = string.Format(FormatRunning, equippedSmartWearableCount);
            smartWearablePxSummary.KilledText.text = string.Format(FormatKilled, killedSmartWearableCount);

            globalPxRunningText.text = string.Format(FormatRunning, runningGlobalPortableExperienceCount);

            localPxSummary.AllowedText.text = string.Format(FormatAllowed, allowedLocalPortableExperienceCount);
            localPxSummary.RunningText.text = string.Format(FormatRunning, runningLocalPortableExperienceCount);
            localPxSummary.KilledText.text = string.Format(FormatKilled, killedLocalPortableExperienceCount);

            titleText.text = string.Format(FormatTitle, equippedSmartWearableCount + runningGlobalPortableExperienceCount + runningLocalPortableExperienceCount);
        }
    }
}
