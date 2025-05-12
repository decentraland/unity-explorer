using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Sections
{
    public class MarketplaceCreditsVerifyEmailSubView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_Text Subtitle { get; private set; }

        [field: SerializeField]
        public Button UpdateEmailButton { get; private set; }

        [field: SerializeField]
        public Button ResendVerificationEmailButton { get; private set; }

        [field: SerializeField]
        public GameObject MainContainer { get; private set; }

        [field: SerializeField]
        public GameObject MainLoadingSpinner { get; private set; }

        public void SetEmailToVerify(string email) =>
            Subtitle.text = $"Almost there! A confirmation email has been sent to <color=#FF2D55><b>{email}</b></color>. Click the confirmation button in the email to proceed.";

        public void SetAsLoading(bool isLoading)
        {
            MainLoadingSpinner.SetActive(isLoading);
            MainContainer.SetActive(!isLoading);
        }
    }
}
