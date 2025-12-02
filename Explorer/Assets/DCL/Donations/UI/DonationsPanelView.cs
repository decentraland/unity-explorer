using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.UI;
using DCL.UI.ConfirmationDialog.Opener;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities.Extensions;
using MVC;
using SceneRunner.Scene;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.Donations.UI
{
    public class DonationsPanelView : ViewBase, IView
    {
        private const string MANA_EQUIVALENT_FORMAT = "(${0:0.00} USD)";
        private const string SEND_CONFIRMATION_TEXT_FORMAT = "Are you sure you want to send {0} MANA to {1}?";
        private const string SEND_DONATION_CONFIRM_TEXT = "YES";
        private const string SEND_DONATION_CANCEL_TEXT = "NO";

        public event Action<string, decimal>? SendDonationRequested;

        [field: Header("References")]
        [field: SerializeField] private Button cancelButton { get; set; } = null!;
        [field: SerializeField] private Button sendButton { get; set; } = null!;
        [field: SerializeField] private SkeletonLoadingView loadingView { get; set; } = null!;

        [field: Header("Scene")]
        [field: SerializeField] private TMP_Text sceneNameText { get; set; } = null!;

        [field: Header("Creator")]
        [field: SerializeField] private ProfilePictureView profilePictureView { get; set; } = null!;
        [field: SerializeField] private SimpleUserNameElement userNameElement { get; set; } = null!;
        [field: Space(5)]
        [field: SerializeField] private UserWalletAddressElement creatorAddressElement { get; set; } = null!;

        [field: Header("Donation")]
        [field: SerializeField] private TMP_Text currentBalanceText { get; set; } = null!;
        [field: SerializeField] private TMP_InputField donationInputField { get; set; } = null!;
        [field: SerializeField] private Image donationBorderError { get; set; } = null!;
        [field: SerializeField] private TMP_Text usdEquivalentText { get; set; } = null!;

        private readonly UniTask[] closingTasks = new UniTask[2];

        private UserWalletAddressElementController? creatorAddressController;
        private decimal manaUsdConversion;
        private CancellationTokenSource confirmationCts = new ();

        private void Awake()
        {
            creatorAddressController = new UserWalletAddressElementController(creatorAddressElement);

            donationInputField.onValueChanged.AddListener(OnValueChanged);
            donationInputField.onEndEdit.AddListener(OnEndEdit);
        }

        public void SetLoadingState(bool active)
        {
            if (active)
                loadingView.ShowLoading();
            else
                loadingView.HideLoading();
        }

        public void PlayWaitAnimation()
        {

        }

        public void StopWaitAnimation()
        {

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
            sceneNameText.text = sceneName;

            profilePictureView.gameObject.SetActive(profile != null);
            userNameElement.gameObject.SetActive(profile != null);

            if (profile != null)
            {
                profilePictureView.Setup(profileRepositoryWrapper, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl);
                userNameElement.Setup(profile);
            }

            creatorAddressController!.Setup(sceneCreatorAddress);

            currentBalanceText.text = currentBalance.ToString("0.00");
            donationInputField.text = suggestedDonationAmount.ToString("0.00");

            confirmationCts = confirmationCts.SafeRestart();

            sendButton.onClick.RemoveAllListeners();
            sendButton.onClick.AddListener( () => OpenConfirmationDialogAsync(sceneCreatorAddress, decimal.Parse(donationInputField.text), confirmationCts.Token));
        }

        private async UniTaskVoid OpenConfirmationDialogAsync(string creatorAddress, decimal amount, CancellationToken ct)
        {
            var result = await ViewDependencies.ConfirmationDialogOpener.OpenConfirmationDialogAsync(
                                                    new ConfirmationDialogParameter(string.Format(SEND_CONFIRMATION_TEXT_FORMAT, amount, creatorAddress), SEND_DONATION_CANCEL_TEXT, SEND_DONATION_CONFIRM_TEXT,
                                                        null, false, false), ct)
                                               .SuppressToResultAsync(ReportCategory.DONATIONS);

            if (!result.Success || result.Value == ConfirmationResult.CANCEL || ct.IsCancellationRequested)
                return;

            SendDonationRequested?.Invoke(creatorAddress, amount);
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
            bool isValid = decimal.TryParse(value, out decimal number) && number >= 1;
            sendButton.interactable = isValid;
            donationBorderError.color = isValid ? Color.white : Color.softRed;

            if (isValid)
                usdEquivalentText.text = string.Format(MANA_EQUIVALENT_FORMAT, number * manaUsdConversion);
        }

        public UniTask[] GetClosingTasks(UniTask controllerTask, CancellationToken ct)
        {
            closingTasks[0] = cancelButton.OnClickAsync(ct);
            closingTasks[1] = controllerTask;

            return closingTasks;
        }
    }
}
