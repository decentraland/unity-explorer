using DCL.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Fields
{
    public class MarketplaceCreditsTotalCreditsWidgetView : MonoBehaviour
    {
        private static readonly int CREDITS_CLAIMED_TRIGGER = Animator.StringToHash("Claimed");

        [field: SerializeField]
        public GameObject TotalCreditsContainer { get; private set; }

        [field: SerializeField]
        public TMP_Text TotalCreditsText { get; private set; }

        [field: SerializeField]
        public TMP_Text DaysToExpireText { get; private set; }

        [field: SerializeField]
        public Button GoShoppingButton { get; private set; }

        [field: SerializeField]
        public Image GoShoppingMainBackground { get; private set; }

        [field: SerializeField]
        public Image GoShoppingProgramEndBackground { get; private set; }

        [field: SerializeField]
        public Animator TotalCreditsAnimator { get; private set; }

        [field: SerializeField]
        public AudioClipConfig ClaimedSound { get; private set; }

        public void SetCredits(string creditsText) =>
            TotalCreditsText.text = creditsText;

        public void SetDaysToExpire(string daysToExpireText) =>
            DaysToExpireText.text = daysToExpireText;

        public void SetDaysToExpireVisible(bool isVisible) =>
            DaysToExpireText.gameObject.SetActive(isVisible);

        public void SetAsProgramEndVersion(bool isProgramEndVersion)
        {
            GoShoppingMainBackground.gameObject.SetActive(!isProgramEndVersion);
            GoShoppingProgramEndBackground.gameObject.SetActive(isProgramEndVersion);
        }

        public void PlayClaimCreditsAnimation()
        {
            TotalCreditsAnimator.SetTrigger(CREDITS_CLAIMED_TRIGGER);
            UIAudioEventsBus.Instance.SendPlayAudioEvent(ClaimedSound);
        }
    }
}
