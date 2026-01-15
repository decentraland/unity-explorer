using DCL.UI.ValidatedInputField;
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
        public GameObject EmailLoginContainer { get; private set; }

        [field: SerializeField]
        public ValidatedInputFieldView EmailInputField { get; private set; }

        [field: SerializeField]
        public Button StartWithEmailButton { get; private set; }

        public bool IsEmailLoginActive
        {
            get => EmailLoginContainer.activeSelf;

            set
            {
                EmailLoginContainer.SetActive(value);
                StartButton.gameObject.SetActive(!value);
            }
        }

        public void SetAsLoading(bool isLoading)
        {
            ContentContainer.SetActive(!isLoading);
            LoadingContainer.SetActive(isLoading);
        }

        public void ShowEmailError(bool show) =>
            EmailInputField.ShowError(show);

        public void CleanEmailInput() =>
            EmailInputField.Clear();

        public void SetStartWithEmailButtonInteractable(bool isInteractable) =>
            StartWithEmailButton.interactable = isInteractable;
    }
}
