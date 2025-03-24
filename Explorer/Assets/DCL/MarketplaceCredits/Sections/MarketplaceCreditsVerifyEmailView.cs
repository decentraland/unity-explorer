using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Sections
{
    public class MarketplaceCreditsVerifyEmailView : MonoBehaviour
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
            Subtitle.text = $"Almost there! We have sent you an email containing the instructions to verify your identity to <color=#FF2D55>{email}</color>";

        public void SetAsLoading(bool isLoading)
        {
            MainLoadingSpinner.SetActive(isLoading);
            MainContainer.SetActive(!isLoading);
        }
    }
}
