using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using MVC;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Donations.UI
{
    public class DonationsPanelView : ViewBase, IView
    {
        private enum SubViews
        {
            DEFAULT,
            LOADING,
            TX_CONFIRMED,
            ERROR
        }

        public event Action<string, decimal>? SendDonationRequested;
        public event Action? BuyMoreRequested;
        public event Action? ContactSupportRequested;

        [field: Header("References")]
        [field: SerializeField] private DonationDefaultView donationDefaultView { get; set; } = null!;
        [field: SerializeField] private DonationConfirmedView donationConfirmedView { get; set; } = null!;
        [field: SerializeField] private DonationErrorView donationErrorView { get; set; } = null!;
        [field: SerializeField] private DonationLoadingView donationLoadingView { get; set; } = null!;

        private readonly UniTask[] closingTasks = new UniTask[4];

        private void Awake()
        {
            donationDefaultView.buyMoreManaButton.onClick.AddListener(() => BuyMoreRequested?.Invoke());

            donationErrorView.contactSupportButton.onClick.AddListener(() => ContactSupportRequested?.Invoke());
            donationErrorView.tryAgainButton.onClick.AddListener(() => ShowSubView(SubViews.DEFAULT));

            donationDefaultView.SendDonationRequested += (address, amount) => SendDonationRequested?.Invoke(address, amount);
        }

        public void SetDefaultLoadingState(bool active)
        {
            ShowSubView(SubViews.DEFAULT);

            if (active)
                donationDefaultView.loadingView.ShowLoading(true);
            else
                donationDefaultView.loadingView.HideLoading();
        }

        public void ShowLoading(Profile? profile, string creatorAddress, decimal donationAmount, ProfileRepositoryWrapper profileRepositoryWrapper)
        {
            ShowSubView(SubViews.LOADING);
            donationLoadingView.ConfigurePanel(profile, creatorAddress, donationAmount, profileRepositoryWrapper);
        }

        public void ShowErrorModal()
        {
            ShowSubView(SubViews.ERROR);
        }

        private void ShowSubView(SubViews newSubView)
        {
            donationDefaultView.gameObject.SetActive(newSubView == SubViews.DEFAULT);
            donationConfirmedView.gameObject.SetActive(newSubView == SubViews.TX_CONFIRMED);
            donationErrorView.gameObject.SetActive(newSubView == SubViews.ERROR);
            donationLoadingView.gameObject.SetActive(newSubView == SubViews.LOADING);
        }

        public async UniTask ShowTxConfirmedAsync(Profile? profile, string creatorAddress, CancellationToken ct, ProfileRepositoryWrapper profileRepositoryWrapper)
        {
            ShowSubView(SubViews.TX_CONFIRMED);

            await donationConfirmedView.ShowAsync(profile, creatorAddress, ct, profileRepositoryWrapper);
        }

        public void ConfigureDefaultPanel(Profile? profile,
            string sceneCreatorAddress,
            string sceneName,
            decimal currentBalance,
            decimal[] suggestedDonationAmount,
            decimal manaUsdPrice,
            ProfileRepositoryWrapper profileRepositoryWrapper)
        {
            donationDefaultView.ConfigurePanel(profile, sceneCreatorAddress, sceneName, currentBalance, suggestedDonationAmount, manaUsdPrice, profileRepositoryWrapper);
        }

        public UniTask[] GetClosingTasks(UniTask controllerTask, CancellationToken ct)
        {
            closingTasks[0] = donationDefaultView.cancelButton.OnClickAsync(ct);
            closingTasks[1] = controllerTask;
            closingTasks[2] = donationErrorView.closeButton.OnClickAsync(ct);
            closingTasks[3] = donationDefaultView.skeletonCancelButton.OnClickAsync(ct);

            return closingTasks;
        }
    }
}
