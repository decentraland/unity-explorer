using Cysharp.Threading.Tasks;
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

        public event Action<DonationPanelViewModel, decimal>? SendDonationRequested;
        public event Action? BuyMoreRequested;
        public event Action? ContactSupportRequested;

        [field: Header("References")]
        [field: SerializeField] private DonationDefaultView donationDefaultView { get; set; } = null!;
        [field: SerializeField] private DonationConfirmedView donationConfirmedView { get; set; } = null!;
        [field: SerializeField] private DonationErrorView donationErrorView { get; set; } = null!;
        [field: SerializeField] private DonationLoadingView donationLoadingView { get; set; } = null!;

        [field: Header("Assets")]
        [field: SerializeField] internal Sprite defaultProfileThumbnail;

        private readonly UniTask[] closingTasks = new UniTask[4];

        private void Awake()
        {
            donationDefaultView.buyMoreManaButton.onClick.AddListener(() => BuyMoreRequested?.Invoke());

            donationErrorView.contactSupportButton.onClick.AddListener(() => ContactSupportRequested?.Invoke());
            donationErrorView.tryAgainButton.onClick.AddListener(() => ShowSubView(SubViews.DEFAULT));

            donationDefaultView.SendDonationRequested += (vm, amount) => SendDonationRequested?.Invoke(vm, amount);
        }

        public void SetDefaultLoadingState(bool active)
        {
            ShowSubView(SubViews.DEFAULT);

            if (active)
                donationDefaultView.loadingView.ShowLoading(true);
            else
                donationDefaultView.loadingView.HideLoading();
        }

        public void ShowLoading(DonationPanelViewModel viewModel, decimal donationAmount)
        {
            ShowSubView(SubViews.LOADING);
            donationLoadingView.ConfigurePanel(viewModel, donationAmount);
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

        public async UniTask ShowTxConfirmedAsync(DonationPanelViewModel viewModel, CancellationToken ct)
        {
            ShowSubView(SubViews.TX_CONFIRMED);

            await donationConfirmedView.ShowAsync(viewModel, ct);
        }

        public void ConfigureDefaultPanel(DonationPanelViewModel viewModel)
        {
            donationDefaultView.ConfigurePanel(viewModel);
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
