using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits
{
    public class MarketplaceCreditsWelcomeView : MonoBehaviour
    {
        [field: SerializeField]
        public Button StartButton { get; private set; }

        [field: SerializeField]
        public Button LearnMoreLinkButton { get; private set; }

        [field: SerializeField]
        public GameObject EmailLoginContainer { get; private set; }

        [field: SerializeField]
        public TMP_InputField EmailInput { get; private set; }

        [field: SerializeField]
        public GameObject EmailErrorOutline { get; private set; }

        [field: SerializeField]
        public GameObject EmailErrorMark { get; private set; }

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

        public void ShowEmailError(bool show)
        {
            EmailErrorOutline.SetActive(show);
            EmailErrorMark.SetActive(show);
        }

        public void CleanEmail() =>
            EmailInput.text = string.Empty;

        public void SetStartWithEmailButtonInteractable(bool isInteractable) =>
            StartWithEmailButton.interactable = isInteractable;
    }
}
