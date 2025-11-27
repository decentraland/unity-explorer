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

        public event Action<string, float>? SendDonationRequested;

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
        private float mansUsdConversion;
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

        public void ConfigurePanel(Profile? profile,
            ISceneFacade sceneFacade,
            float currentBalance,
            float suggestedDonationAmount,
            float manaUsdPrice,
            ProfileRepositoryWrapper profileRepositoryWrapper)
        {
            mansUsdConversion = manaUsdPrice;
            sceneNameText.text = sceneFacade.Info.Name;

            profilePictureView.gameObject.SetActive(profile != null);
            userNameElement.gameObject.SetActive(profile != null);
            creatorAddressElement.gameObject.SetActive(profile == null);

            if (profile != null)
            {
                profilePictureView.Setup(profileRepositoryWrapper, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl);
                userNameElement.Setup(profile);
            }
            else
                creatorAddressController!.Setup(sceneFacade.SceneData.GetCreatorAddress()!);

            currentBalanceText.text = currentBalance.ToString("0.00");
            ((TMP_Text)donationInputField.placeholder).text = suggestedDonationAmount.ToString("0.00");

            confirmationCts = confirmationCts.SafeRestart();

            sendButton.onClick.RemoveAllListeners();
            sendButton.onClick.AddListener( () => OpenConfirmationDialogAsync(sceneFacade.SceneData.GetCreatorAddress()!, float.Parse(donationInputField.text), confirmationCts.Token));
        }

        private async UniTaskVoid OpenConfirmationDialogAsync(string creatorAddress, float amount, CancellationToken ct)
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
            bool isValid = float.TryParse(value, out float number) && number >= 1;
            sendButton.interactable = isValid;
            donationBorderError.color = isValid ? Color.softRed : Color.white;

            if (isValid)
                usdEquivalentText.text = string.Format(MANA_EQUIVALENT_FORMAT, number * mansUsdConversion);
        }

        public UniTask[] GetClosingTasks(UniTask controllerTask, CancellationToken ct)
        {
            closingTasks[0] = cancelButton.OnClickAsync(ct);
            closingTasks[1] = controllerTask;

            return closingTasks;
        }
    }
}
