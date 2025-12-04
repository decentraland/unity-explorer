using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.UI;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using MVC;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Donations.UI
{
    public class DonationsPanelView : ViewBase, IView
    {
        private enum State
        {
            DEFAULT,
            LOADING,
            TX_CONFIRMED,
            ERROR
        }

        private const string MANA_EQUIVALENT_FORMAT = "${0:#.##}";
        private const string DECIMAL_FORMAT = "#.##";
        private const int FIRST_RECOMMENDATION_INDEX = 0;
        private const int SECOND_RECOMMENDATION_INDEX = 1;
        private const int THIRD_RECOMMENDATION_INDEX = 2;
        private const int OTHER_RECOMMENDATION_INDEX = 3;

        public event Action<string, decimal>? SendDonationRequested;
        public event Action? BuyMoreRequested;
        public event Action? ContactSupportRequested;

        [field: Header("References")]
        [field: SerializeField] private Button cancelButton { get; set; } = null!;
        [field: SerializeField] private Button skeletonCancelButton { get; set; } = null!;
        [field: SerializeField] private Button sendButton { get; set; } = null!;
        [field: SerializeField] private SkeletonLoadingView loadingView { get; set; } = null!;
        [field: SerializeField] private DonationConfirmedView donationConfirmedView { get; set; } = null!;
        [field: SerializeField] private DonationErrorView donationErrorView { get; set; } = null!;
        [field: SerializeField] private DonationLoadingView donationLoadingView { get; set; } = null!;

        [field: Header("Scene")]
        [field: SerializeField] private TMP_Text sceneNameText { get; set; } = null!;

        [field: Header("Creator")]
        [field: SerializeField] private ProfilePictureView profilePictureView { get; set; } = null!;
        [field: SerializeField] private SimpleUserNameElement userNameElement { get; set; } = null!;
        [field: Space(5)]
        [field: SerializeField] private UserWalletAddressElement creatorAddressElement { get; set; } = null!;
        [field: SerializeField] private Color NoProfileColor { get; set; }

        [field: Header("Donation")]
        [field: SerializeField] private TMP_Text currentBalanceText { get; set; } = null!;
        [field: SerializeField] private TMP_Text manaAvailableText { get; set; } = null!;
        [field: SerializeField] private Image manaAvailableIcon { get; set; } = null!;
        [field: SerializeField] private TMP_InputField donationInputField { get; set; } = null!;
        [field: SerializeField] private Image donationBorderError { get; set; } = null!;
        [field: SerializeField] private TMP_Text usdEquivalentText { get; set; } = null!;
        [field: SerializeField] private Color InvalidColor { get; set; }
        [field: SerializeField] private Button BuyMoreMANAButton { get; set; }
        [field: SerializeField] private GameObject BalanceWarningIcon { get; set; }

        [field: Header("Donation recommendations")]
        [field: SerializeField] private ButtonWithSelectableStateView[] recommendationButtons { get; set; }

        private readonly UniTask[] closingTasks = new UniTask[4];

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
            BuyMoreMANAButton.onClick.AddListener(() => BuyMoreRequested?.Invoke());
            donationErrorView.contactSupportButton.onClick.AddListener(() => ContactSupportRequested?.Invoke());
            donationErrorView.tryAgainButton.onClick.AddListener(() => ChangeState(State.DEFAULT));

            recommendationButtons[FIRST_RECOMMENDATION_INDEX].Button.onClick.AddListener(() => ManageRecommendationClick(FIRST_RECOMMENDATION_INDEX));
            recommendationButtons[SECOND_RECOMMENDATION_INDEX].Button.onClick.AddListener(() => ManageRecommendationClick(SECOND_RECOMMENDATION_INDEX));
            recommendationButtons[THIRD_RECOMMENDATION_INDEX].Button.onClick.AddListener(() => ManageRecommendationClick(THIRD_RECOMMENDATION_INDEX));
            recommendationButtons[OTHER_RECOMMENDATION_INDEX].Button.onClick.AddListener(() => ManageRecommendationClick(OTHER_RECOMMENDATION_INDEX));
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

        public void SetLoadingState(bool active)
        {
            ChangeState(State.DEFAULT);

            if (active)
                loadingView.ShowLoading();
            else
                loadingView.HideLoading();
        }

        public void ShowLoading(Profile? profile, string creatorAddress, decimal donationAmount, ProfileRepositoryWrapper profileRepositoryWrapper)
        {
            ChangeState(State.LOADING);
            donationLoadingView.ConfigurePanel(profile, creatorAddress, donationAmount, profileRepositoryWrapper);
        }

        public void ShowErrorModal()
        {
            ChangeState(State.ERROR);
        }

        private void ChangeState(State newState)
        {
            loadingView.gameObject.SetActive(newState == State.DEFAULT);
            donationConfirmedView.gameObject.SetActive(newState == State.TX_CONFIRMED);
            donationErrorView.gameObject.SetActive(newState == State.ERROR);
            donationLoadingView.gameObject.SetActive(newState == State.LOADING);
        }

        public async UniTask ShowTxConfirmedAsync(Profile? profile, string creatorAddress, CancellationToken ct, ProfileRepositoryWrapper profileRepositoryWrapper)
        {
            ChangeState(State.TX_CONFIRMED);

            await donationConfirmedView.ShowAsync(profile, creatorAddress, ct, profileRepositoryWrapper);
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
                profilePictureView.SetBackgroundColor(NoProfileColor);
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

            if (number >= currentBalance)
            {
                BalanceWarningIcon.SetActive(true);
                currentBalanceText.color = InvalidColor;
                manaAvailableText.color = InvalidColor;
                manaAvailableIcon.color = InvalidColor;
            }
            else
            {
                BalanceWarningIcon.SetActive(false);
                currentBalanceText.color = manaAvailableOriginalColor;
                manaAvailableText.color = manaAvailableOriginalColor;
                manaAvailableIcon.color = manaAvailableOriginalColor;
            }

            usdEquivalentText.text = string.Format(MANA_EQUIVALENT_FORMAT, number * manaUsdConversion);
            donationBorderError.color = isValid ? donationBorderOriginalColor : InvalidColor;
        }

        public UniTask[] GetClosingTasks(UniTask controllerTask, CancellationToken ct)
        {
            closingTasks[0] = cancelButton.OnClickAsync(ct);
            closingTasks[1] = controllerTask;
            closingTasks[2] = donationErrorView.closeButton.OnClickAsync(ct);
            closingTasks[3] = skeletonCancelButton.OnClickAsync(ct);

            return closingTasks;
        }
    }
}
