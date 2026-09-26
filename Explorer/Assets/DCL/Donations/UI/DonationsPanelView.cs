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
            Default,
            Loading,
            TxConfirmed,
            Error
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
            donationErrorView.tryAgainButton.onClick.AddListener(() => ShowSubView(SubViews.Default));

            donationDefaultView.SendDonationRequested += (vm, amount) => SendDonationRequested?.Invoke(vm, amount);
        }

        public void SetDefaultLoadingState(bool active)
        {
            ShowSubView(SubViews.Default);

            if (active)
                donationDefaultView.loadingView.ShowLoading(true);
            else
                donationDefaultView.loadingView.HideLoading();
        }

        public void ShowLoading(DonationPanelViewModel viewModel, decimal donationAmount, bool isThirdWeb = false)
        {
            ShowSubView(SubViews.Loading);
            donationLoadingView.SetWaitingMessage(isThirdWeb);
            donationLoadingView.ConfigurePanel(viewModel, donationAmount);
        }

        public void ShowErrorModal()
        {
            ShowSubView(SubViews.Error);
        }

        private void ShowSubView(SubViews newSubView)
        {
            donationDefaultView.gameObject.SetActive(newSubView == SubViews.Default);
            donationConfirmedView.gameObject.SetActive(newSubView == SubViews.TxConfirmed);
            donationErrorView.gameObject.SetActive(newSubView == SubViews.Error);
            donationLoadingView.gameObject.SetActive(newSubView == SubViews.Loading);
        }

        public async UniTask ShowTxConfirmedAsync(DonationPanelViewModel viewModel, CancellationToken ct)
        {
            ShowSubView(SubViews.TxConfirmed);

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
