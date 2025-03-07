using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits
{
    public class MarketplaceCreditsGoalRowView : MonoBehaviour
    {
        [field: SerializeField]
        public Image GoalImage { get; private set; }

        [field: SerializeField]
        public TMP_Text GoalTitleText { get; private set; }

        [field: SerializeField]
        public RectTransform ProgressBar { get; private set; }

        [field: SerializeField]
        public RectTransform ProgressBarFill { get; private set; }

        [field: SerializeField]
        public TMP_Text ProgressValueText { get; private set; }

        [field: SerializeField]
        public TMP_Text GoalCreditsValueText { get; private set; }
    }
}
