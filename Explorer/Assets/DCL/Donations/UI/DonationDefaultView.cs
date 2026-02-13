using DCL.UI;
using DCL.UI.ProfileElements;
using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Donations.UI
{
    public class DonationDefaultView : MonoBehaviour
    {
        private const string MANA_AVAILABLE_NORMAL = "Available";
        private const string MANA_AVAILABLE_ERROR = "Insufficient MANA";
        private const string DECIMAL_FORMAT = "0.##";
        private const int FIRST_RECOMMENDATION_INDEX = 0;
        private const int SECOND_RECOMMENDATION_INDEX = 1;
        private const int THIRD_RECOMMENDATION_INDEX = 2;
        private const int OTHER_RECOMMENDATION_INDEX = 3;

        public event Action<DonationPanelViewModel?, decimal>? SendDonationRequested;

        [field: Header("References")]
        [field: SerializeField] internal Button cancelButton { get; set; } = null!;
        [field: SerializeField] internal Button skeletonCancelButton { get; set; } = null!;
        [field: SerializeField] private Button sendButton { get; set; } = null!;
        [field: SerializeField] internal SkeletonLoadingView loadingView { get; set; } = null!;

        [field: Header("Scene")]
        [field: SerializeField] private TMP_Text sceneNameText { get; set; } = null!;

        [field: Header("Creator")]
        [field: SerializeField] private ProfilePictureView profilePictureView { get; set; } = null!;
        [field: SerializeField] private SimpleUserNameElement userNameElement { get; set; } = null!;
        [field: Space(5)]
        [field: SerializeField] private UserWalletAddressElement creatorAddressElement { get; set; } = null!;
        [field: SerializeField] private Color noProfileColor { get; set; }

        [field: Header("Donation")]
        [field: SerializeField] private TMP_Text currentBalanceText { get; set; } = null!;
        [field: SerializeField] private TMP_Text manaAvailableText { get; set; } = null!;
        [field: SerializeField] private Image manaAvailableIcon { get; set; } = null!;
        [field: SerializeField] private TMP_InputField donationInputFieldMana { get; set; } = null!;
        [field: SerializeField] private TMP_InputField donationInputFieldUsd { get; set; } = null!;
        [field: SerializeField] private Image[] donationBorderError { get; set; } = null!;
        [field: SerializeField] private Color invalidColor { get; set; }
        [field: SerializeField] internal Button buyMoreManaButton { get; set; } = null!;
        [field: SerializeField] private GameObject balanceWarningIcon { get; set; } = null!;
        [field: SerializeField] private GameObject balanceManaIcon { get; set; } = null!;
        [field: SerializeField] private GameObject donationErrorTip { get; set; } = null!;

        [field: Header("Donation recommendations")]
        [field: SerializeField] private ButtonWithSelectableStateView[] recommendationButtons { get; set; } = null!;

        private UserWalletAddressElementPresenter? creatorAddressController;
        private DonationPanelViewModel? currentViewModel;
        private Color donationBorderOriginalColor;
        private Color manaAvailableOriginalColor;


        private void Awake()
        {
            creatorAddressController = new UserWalletAddressElementPresenter(creatorAddressElement);
            donationBorderOriginalColor = donationBorderError[0].color;
            manaAvailableOriginalColor = manaAvailableText.color;

            donationInputFieldMana.onValueChanged.AddListener(OnManaValueChanged);
            donationInputFieldUsd.onValueChanged.AddListener(OnUsdValueChanged);

            recommendationButtons[FIRST_RECOMMENDATION_INDEX].Button.onClick.AddListener(() => ManageRecommendationClick(FIRST_RECOMMENDATION_INDEX));
            recommendationButtons[SECOND_RECOMMENDATION_INDEX].Button.onClick.AddListener(() => ManageRecommendationClick(SECOND_RECOMMENDATION_INDEX));
            recommendationButtons[THIRD_RECOMMENDATION_INDEX].Button.onClick.AddListener(() => ManageRecommendationClick(THIRD_RECOMMENDATION_INDEX));
            recommendationButtons[OTHER_RECOMMENDATION_INDEX].Button.onClick.AddListener(() => ManageRecommendationClick(OTHER_RECOMMENDATION_INDEX));
        }

        public void ConfigurePanel(DonationPanelViewModel viewModel)
        {
            currentViewModel = viewModel;
            sceneNameText.text = viewModel.SceneName;

            userNameElement.gameObject.SetActive(viewModel.Profile != null);

            profilePictureView.Bind(viewModel.ProfileThumbnail);

            if (viewModel.Profile.HasValue)
            {
                userNameElement.Setup(viewModel.Profile.Value);
                profilePictureView.ConfigureThumbnailClickData(userAddress: viewModel.SceneCreatorAddress);
            }

            creatorAddressController!.Setup(viewModel.SceneCreatorAddress);

            currentBalanceText.text = viewModel.CurrentBalance.ToString(DECIMAL_FORMAT);

            ManageRecommendationClick(FIRST_RECOMMENDATION_INDEX);

            recommendationButtons[FIRST_RECOMMENDATION_INDEX].Text.text = viewModel.SuggestedDonationAmount[FIRST_RECOMMENDATION_INDEX].ToString(DECIMAL_FORMAT);
            recommendationButtons[SECOND_RECOMMENDATION_INDEX].Text.text = viewModel.SuggestedDonationAmount[SECOND_RECOMMENDATION_INDEX].ToString(DECIMAL_FORMAT);
            recommendationButtons[THIRD_RECOMMENDATION_INDEX].Text.text = viewModel.SuggestedDonationAmount[THIRD_RECOMMENDATION_INDEX].ToString(DECIMAL_FORMAT);

            sendButton.onClick.RemoveAllListeners();
            sendButton.onClick.AddListener( TriggerSendDonationRequested);
        }

        private void TriggerSendDonationRequested() =>
            SendDonationRequested?.Invoke(currentViewModel, decimal.Parse(donationInputFieldMana.text));

        public void ManaOverlayInputClicked()
        {
            if (donationInputFieldMana.interactable) return;

            ManageRecommendationClick(OTHER_RECOMMENDATION_INDEX);
        }

        public void UsdOverlayInputClicked()
        {
            if (donationInputFieldUsd.interactable) return;

            ManageRecommendationClick(OTHER_RECOMMENDATION_INDEX);
        }

        private void ManageRecommendationClick(int index)
        {
            donationInputFieldMana.interactable = index == OTHER_RECOMMENDATION_INDEX;
            donationInputFieldUsd.interactable = index == OTHER_RECOMMENDATION_INDEX;

            foreach (var border in donationBorderError)
                border.gameObject.SetActive(index == OTHER_RECOMMENDATION_INDEX);

            if (donationInputFieldMana.interactable)
                donationInputFieldMana.OnSelect(null);

            for (var i = 0; i < recommendationButtons.Length; i++)
            {
                recommendationButtons[i].SetSelected(i == index);

                if (i == index && i != OTHER_RECOMMENDATION_INDEX)
                    donationInputFieldMana.text = currentViewModel?.SuggestedDonationAmount[i].ToString(DECIMAL_FORMAT);
            }
        }

        private void OnManaValueChanged(string value)
        {
            string newValue = value.Replace("-", "");
            bool parsedValueSuccess = decimal.TryParse(newValue, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsedValue);

            donationInputFieldMana.SetTextWithoutNotify(newValue);
            donationInputFieldUsd.SetTextWithoutNotify((parsedValueSuccess ? parsedValue * currentViewModel.ManaUsdPrice : 0).ToString(DECIMAL_FORMAT));

            ValidateManaValue(donationInputFieldMana.text);
        }

        private void OnUsdValueChanged(string value)
        {
            string newValue = value.Replace("-", "");
            bool parsedValueSuccess = decimal.TryParse(newValue, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsedValue);

            donationInputFieldUsd.SetTextWithoutNotify(newValue);
            donationInputFieldMana.SetTextWithoutNotify((parsedValueSuccess ? parsedValue / currentViewModel.ManaUsdPrice : 0).ToString(DECIMAL_FORMAT));

            ValidateManaValue(donationInputFieldMana.text);
        }

        private void ValidateManaValue(string value)
        {
            bool isValid = decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number) && number >= 1 && number <= currentViewModel.CurrentBalance;
            sendButton.interactable = isValid;

            donationErrorTip.gameObject.SetActive(number < 1);

            if (number >= currentViewModel.CurrentBalance)
            {
                balanceWarningIcon.SetActive(true);
                balanceManaIcon.SetActive(false);
                currentBalanceText.color = invalidColor;
                manaAvailableText.color = invalidColor;
                manaAvailableIcon.color = invalidColor;
                manaAvailableText.text = MANA_AVAILABLE_ERROR;
            }
            else
            {
                balanceWarningIcon.SetActive(false);
                balanceManaIcon.SetActive(true);
                currentBalanceText.color = manaAvailableOriginalColor;
                manaAvailableText.color = manaAvailableOriginalColor;
                manaAvailableIcon.color = manaAvailableOriginalColor;
                manaAvailableText.text = MANA_AVAILABLE_NORMAL;
            }

            foreach (var border in donationBorderError)
                border.color = isValid ? donationBorderOriginalColor : invalidColor;
        }
    }
}
