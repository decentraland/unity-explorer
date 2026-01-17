using DCL.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Sections
{
    public class MarketplaceCreditsWelcomeSubView : MonoBehaviour
    {
        [field: SerializeField]
        public GameObject ContentContainer { get; private set; }

        [field: SerializeField]
        public GameObject LoadingContainer { get; private set; }

        [field: SerializeField]
        public Button StartButton { get; private set; }

        [field: SerializeField]
        public Button LearnMoreLinkButton { get; private set; }

        [field: SerializeField]
        public EmailInputFieldView EmailLogin { get; private set; }

        public void SetEmailLoginVisibility(bool isVisible)
        {
            EmailLogin.gameObject.SetActive(isVisible);
            StartButton.gameObject.SetActive(!isVisible);
        }

        public void SetAsLoading(bool isLoading)
        {
            ContentContainer.SetActive(!isLoading);
            LoadingContainer.SetActive(isLoading);
        }
    }
}
