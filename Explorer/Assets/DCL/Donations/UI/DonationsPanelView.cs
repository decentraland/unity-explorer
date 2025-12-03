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

        private const string MANA_EQUIVALENT_FORMAT = "${0:0.00}";

        public event Action<string, decimal>? SendDonationRequested;
        public event Action? BuyMoreRequested;

        [field: Header("References")]
        [field: SerializeField] private Button cancelButton { get; set; } = null!;
        [field: SerializeField] private Button sendButton { get; set; } = null!;
        [field: SerializeField] private SkeletonLoadingView loadingView { get; set; } = null!;
        [field: SerializeField] private DonationConfirmedView donationConfirmedView { get; set; } = null!;

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
        [field: SerializeField] private TMP_InputField donationInputField { get; set; } = null!;
        [field: SerializeField] private Image donationBorderError { get; set; } = null!;
        [field: SerializeField] private TMP_Text usdEquivalentText { get; set; } = null!;
        [field: SerializeField] private Color InvalidColor { get; set; }
        [field: SerializeField] private Button BuyMoreMANAButton { get; set; }
        [field: SerializeField] private GameObject BalanceWarningIcon { get; set; }

        private readonly UniTask[] closingTasks = new UniTask[2];

        private UserWalletAddressElementController? creatorAddressController;
        private decimal manaUsdConversion;
        private decimal currentBalance;
        private Color donationBorderOriginalColor;
        private Color manaAvailableOriginalColor;

        private void Awake()
        {
            creatorAddressController = new UserWalletAddressElementController(creatorAddressElement);
            donationBorderOriginalColor = donationBorderError.color;
            manaAvailableOriginalColor = manaAvailableText.color;

            donationInputField.onValueChanged.AddListener(OnValueChanged);
            donationInputField.onEndEdit.AddListener(OnEndEdit);
            BuyMoreMANAButton.onClick.AddListener(() => BuyMoreRequested?.Invoke());
        }

        public void SetLoadingState(bool active)
        {
            ChangeState(State.DEFAULT);

            if (active)
                loadingView.ShowLoading();
            else
                loadingView.HideLoading();
        }

        public void ShowLoading()
        {
            //ChangeState(State.LOADING);
        }

        private void ChangeState(State newState)
        {
            loadingView.gameObject.SetActive(newState == State.DEFAULT);
            donationConfirmedView.gameObject.SetActive(newState == State.TX_CONFIRMED);
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
            decimal suggestedDonationAmount,
            decimal manaUsdPrice,
            ProfileRepositoryWrapper profileRepositoryWrapper)
        {
            manaUsdConversion = manaUsdPrice;
            this.currentBalance = currentBalance;
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

            currentBalanceText.text = currentBalance.ToString("0.00");
            donationInputField.text = suggestedDonationAmount.ToString("0.00");

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
            }
            else
            {
                BalanceWarningIcon.SetActive(false);
                currentBalanceText.color = manaAvailableOriginalColor;
                manaAvailableText.color = manaAvailableOriginalColor;
            }

            if (isValid)
            {
                usdEquivalentText.text = string.Format(MANA_EQUIVALENT_FORMAT, number * manaUsdConversion);
                donationBorderError.color = donationBorderOriginalColor;
            }
            else
                donationBorderError.color = InvalidColor;
        }

        public UniTask[] GetClosingTasks(UniTask controllerTask, CancellationToken ct)
        {
            closingTasks[0] = cancelButton.OnClickAsync(ct);
            closingTasks[1] = controllerTask;

            return closingTasks;
        }
    }
}
