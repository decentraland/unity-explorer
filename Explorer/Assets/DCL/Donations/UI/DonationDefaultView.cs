using DCL.Profiles;
using DCL.UI;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Donations.UI
{
    public class DonationDefaultView : MonoBehaviour
    {
        private const string MANA_EQUIVALENT_FORMAT = "${0:0.##}";
        private const string MANA_AVAILABLE_NORMAL = "available";
        private const string MANA_AVAILABLE_ERROR = "insufficient MANA";
        private const string DECIMAL_FORMAT = "0.##";
        private const int FIRST_RECOMMENDATION_INDEX = 0;
        private const int SECOND_RECOMMENDATION_INDEX = 1;
        private const int THIRD_RECOMMENDATION_INDEX = 2;
        private const int OTHER_RECOMMENDATION_INDEX = 3;

        public event Action<string, decimal>? SendDonationRequested;

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
        [field: SerializeField] private TMP_InputField donationInputField { get; set; } = null!;
        [field: SerializeField] private Image donationBorderError { get; set; } = null!;
        [field: SerializeField] private TMP_Text usdEquivalentText { get; set; } = null!;
        [field: SerializeField] private Color invalidColor { get; set; }
        [field: SerializeField] internal Button buyMoreManaButton { get; set; } = null!;
        [field: SerializeField] private GameObject balanceWarningIcon { get; set; } = null!;
        [field: SerializeField] private GameObject balanceManaIcon { get; set; } = null!;
        [field: SerializeField] private GameObject donationErrorTip { get; set; } = null!;

        [field: Header("Donation recommendations")]
        [field: SerializeField] private ButtonWithSelectableStateView[] recommendationButtons { get; set; }

        private UserWalletAddressElementController? creatorAddressController;
        private decimal manaUsdConversion;
        private decimal currentBalance;
        private Color donationBorderOriginalColor;
        private Color manaAvailableOriginalColor;
        private decimal[] suggestedDonationAmount;

        private void Awake()
        {
            creatorAddressController = new UserWalletAddressElementController(creatorAddressElement);
            donationBorderOriginalColor = donationBorderError.color;
            manaAvailableOriginalColor = manaAvailableText.color;

            donationInputField.onValueChanged.AddListener(OnValueChanged);
            donationInputField.onEndEdit.AddListener(OnEndEdit);

            recommendationButtons[FIRST_RECOMMENDATION_INDEX].Button.onClick.AddListener(() => ManageRecommendationClick(FIRST_RECOMMENDATION_INDEX));
            recommendationButtons[SECOND_RECOMMENDATION_INDEX].Button.onClick.AddListener(() => ManageRecommendationClick(SECOND_RECOMMENDATION_INDEX));
            recommendationButtons[THIRD_RECOMMENDATION_INDEX].Button.onClick.AddListener(() => ManageRecommendationClick(THIRD_RECOMMENDATION_INDEX));
            recommendationButtons[OTHER_RECOMMENDATION_INDEX].Button.onClick.AddListener(() => ManageRecommendationClick(OTHER_RECOMMENDATION_INDEX));
        }

        public void ConfigurePanel(Profile? profile,
            string sceneCreatorAddress,
            string sceneName,
            decimal currentBalance,
            decimal[] suggestedDonationAmount,
            decimal manaUsdPrice,
            ProfileRepositoryWrapper profileRepositoryWrapper)
        {
            manaUsdConversion = manaUsdPrice;
            this.currentBalance = currentBalance;
            this.suggestedDonationAmount = suggestedDonationAmount;
            sceneNameText.text = sceneName;

            userNameElement.gameObject.SetActive(profile != null);

            if (profile != null)
            {
                profilePictureView.Setup(profileRepositoryWrapper, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl);
                userNameElement.Setup(profile);
            }
            else
            {
                profilePictureView.SetBackgroundColor(noProfileColor);
                profilePictureView.SetDefaultThumbnail();
            }

            creatorAddressController!.Setup(sceneCreatorAddress);

            currentBalanceText.text = currentBalance.ToString(DECIMAL_FORMAT);

            ManageRecommendationClick(FIRST_RECOMMENDATION_INDEX);

            recommendationButtons[FIRST_RECOMMENDATION_INDEX].Text.text = suggestedDonationAmount[FIRST_RECOMMENDATION_INDEX].ToString(DECIMAL_FORMAT);;
            recommendationButtons[SECOND_RECOMMENDATION_INDEX].Text.text = suggestedDonationAmount[SECOND_RECOMMENDATION_INDEX].ToString(DECIMAL_FORMAT);
            recommendationButtons[THIRD_RECOMMENDATION_INDEX].Text.text = suggestedDonationAmount[THIRD_RECOMMENDATION_INDEX].ToString(DECIMAL_FORMAT);

            sendButton.onClick.RemoveAllListeners();
            sendButton.onClick.AddListener( () => SendDonationRequested?.Invoke(sceneCreatorAddress, decimal.Parse(donationInputField.text)));
        }

        private void ManageRecommendationClick(int index)
        {
            donationInputField.interactable = index == OTHER_RECOMMENDATION_INDEX;

            if (donationInputField.interactable)
                donationInputField.OnSelect(null);

            for (int i = 0; i < recommendationButtons.Length; i++)
            {
                recommendationButtons[i].SetSelected(i == index);

                if (i == index && i != OTHER_RECOMMENDATION_INDEX)
                    donationInputField.text = suggestedDonationAmount[i].ToString(DECIMAL_FORMAT);
            }
        }

        private void OnValueChanged(string value)
        {
            if (value.Contains("-"))
                donationInputField.text = value.Replace("-", "");

            Validate(value);
        }

        private void OnEndEdit(string value)
        {
            Validate(value);
        }

        private void Validate(string value)
        {
            bool isValid = decimal.TryParse(value, out decimal number) && number >= 1 && number <= currentBalance;
            sendButton.interactable = isValid;

            donationErrorTip.gameObject.SetActive(number <= 0);
            usdEquivalentText.gameObject.SetActive(!donationErrorTip.gameObject.activeInHierarchy);

            if (number >= currentBalance)
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

            usdEquivalentText.text = string.Format(MANA_EQUIVALENT_FORMAT, number * manaUsdConversion);
            donationBorderError.color = isValid ? donationBorderOriginalColor : invalidColor;
        }
    }
}
