using TMPro;
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
        public TMP_InputField EmailInput { get; private set; }

        [field: SerializeField]
        public Image EmailInputOutline { get; private set; }

        [field: SerializeField]
        public Color OutlineNormalColor { get; private set; }

        [field: SerializeField]
        public Color OutlineErrorColor { get; private set; }

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

        public void SetAsLoading(bool isLoading)
        {
            ContentContainer.SetActive(!isLoading);
            LoadingContainer.SetActive(isLoading);
        }

        public void ShowEmailError(bool show)
        {
            EmailInputOutline.color = show ? OutlineErrorColor : OutlineNormalColor;
            EmailErrorMark.SetActive(show);
        }

        public void CleanEmailInput() =>
            EmailInput.text = string.Empty;

        public void SetStartWithEmailButtonInteractable(bool isInteractable) =>
            StartWithEmailButton.interactable = isInteractable;
    }
}
