using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Profiles;
using DCL.UI.ProfileElements;
using DCL.Web3.Authenticators;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Arch;

namespace DCL.Donations.UI
{
    public class DonationsPanelController : ControllerBase<DonationsPanelView, DonationsPanelParameter>
    {
        private static readonly URN EMOTE_MONEY_URN = new ("money");
        private static readonly InputMapComponent.Kind[] BLOCKED_INPUTS =
        {
            InputMapComponent.Kind.PLAYER,
            InputMapComponent.Kind.SHORTCUTS,
            InputMapComponent.Kind.CAMERA,
            InputMapComponent.Kind.IN_WORLD_CAMERA,
        };

        private readonly IDonationsService donationsService;
        private readonly IProfileRepository profileRepository;
        private readonly decimal[] recommendedDonationAmount;
        private readonly Entity playerEntity;
        private readonly World world;
        private readonly IWebBrowser webBrowser;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IInputBlock inputBlock;
        private readonly ICompositeWeb3Provider web3Provider;

        private CancellationTokenSource panelLifecycleCts = new ();
        private UniTaskCompletionSource closeIntentCompletionSource = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.POPUP;

        public DonationsPanelController(ViewFactoryMethod viewFactory,
            IDonationsService donationsService,
            IProfileRepository profileRepository,
            World world,
            Entity playerEntity,
            IWebBrowser webBrowser,
            IDecentralandUrlsSource decentralandUrlsSource,
            IInputBlock inputBlock,
            ICompositeWeb3Provider web3Provider,
            decimal[] recommendedDonationAmount)
            : base(viewFactory)
        {
            this.donationsService = donationsService;
            this.profileRepository = profileRepository;
            this.world = world;
            this.playerEntity = playerEntity;
            this.webBrowser = webBrowser;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.inputBlock = inputBlock;
            this.web3Provider = web3Provider;
            this.recommendedDonationAmount = recommendedDonationAmount;
        }

        public override void Dispose()
        {
            panelLifecycleCts.SafeCancelAndDispose();

            if (viewInstance == null) return;

            viewInstance.SendDonationRequested -= OnSendDonationRequestedAsync;
            viewInstance.BuyMoreRequested -= OnBuyMoreRequested;
            viewInstance.ContactSupportRequested -= OnContactSupportRequested;
        }

        private void CloseController() =>
            closeIntentCompletionSource.TrySetResult();

        protected override void OnViewInstantiated()
        {
            viewInstance!.SendDonationRequested += OnSendDonationRequestedAsync;
            viewInstance!.BuyMoreRequested += OnBuyMoreRequested;
            viewInstance!.ContactSupportRequested += OnContactSupportRequested;
        }

        private void OnBuyMoreRequested() =>
            webBrowser.OpenUrl(decentralandUrlsSource.Url(DecentralandUrl.Account));

        private void OnContactSupportRequested() =>
            webBrowser.OpenUrl(decentralandUrlsSource.Url(DecentralandUrl.Help));

        protected override void OnBeforeViewShow()
        {
            panelLifecycleCts = panelLifecycleCts.SafeRestart();
            panelLifecycleCts.Token.ThrowIfCancellationRequested();
            closeIntentCompletionSource = new UniTaskCompletionSource();
            LoadDataAsync(panelLifecycleCts.Token).Forget();
        }

        private async void OnSendDonationRequestedAsync(DonationPanelViewModel viewModel, decimal amount)
        {
            try
            {
                viewInstance!.ShowLoading(viewModel, amount, web3Provider.IsThirdWebOTP);

                bool success = await donationsService.SendDonationAsync(viewModel.SceneCreatorAddress, amount, panelLifecycleCts.Token);

                if (success)
                {
                    await viewInstance.ShowTxConfirmedAsync(viewModel, panelLifecycleCts.Token);
                    PlayEmoteByUrn(EMOTE_MONEY_URN);
                    CloseController();
                }
                else
                    viewInstance!.ShowErrorModal();
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.DONATIONS);
                viewInstance!.ShowErrorModal();
            }
        }

        private void PlayEmoteByUrn(URN emoteUrn)
        {
            world.AddOrSet(playerEntity, new CharacterEmoteIntent
            {
                EmoteId = emoteUrn,
                Spatial = true,
                TriggerSource = TriggerSource.SELF
            });
        }

        private async UniTaskVoid LoadDataAsync(CancellationToken ct)
        {
            try
            {
                viewInstance!.SetDefaultLoadingState(true);

                string creatorAddress;
                Vector2Int baseParcel;

                if (inputData.HasValues)
                {
                    creatorAddress = inputData.CreatorAddress;
                    baseParcel = inputData.BaseParcel;
                }
                else
                {
                    var donationStatus = donationsService.DonationsEnabledCurrentScene.Value;
                    if (!donationStatus.enabled)
                    {
                        CloseController();
                        return;
                    }
                    creatorAddress = donationStatus.creatorAddress!;
                    baseParcel = donationStatus.baseParcel!.Value;
                }

                (Profile.CompactInfo? creatorProfile, decimal currentBalance, decimal manaPriceUsd, string sceneName) =
                    await UniTask.WhenAll(profileRepository.GetCompactAsync(creatorAddress, ct, batchBehaviour: IProfileRepository.FetchBehaviour.ENFORCE_SINGLE_GET),
                        donationsService.GetCurrentBalanceAsync(ct),
                        donationsService.GetCurrentManaConversionAsync(ct),
                        donationsService.GetSceneNameAsync(baseParcel, ct));

                // Scene creators can set a wallet that has nothing to do with DCL, so we can safely log this information to ignore 404s
                if (creatorProfile == null)
                    ReportHub.LogException(new Exception($"Previous 404 on profile {creatorAddress} can be ignored as the wallet might not be stored in catalysts"), ReportCategory.DONATIONS);

                DonationPanelViewModel donationPanelViewModel = new (creatorProfile,
                    creatorAddress,
                    sceneName,
                    currentBalance,
                    recommendedDonationAmount,
                    manaPriceUsd);

                if (creatorProfile != null)
                {
                    donationPanelViewModel.ProfileThumbnail.SetLoading(creatorProfile.Value.UserNameColor);

                    GetProfileThumbnailCommand.Instance.ExecuteAsync(donationPanelViewModel.ProfileThumbnail, viewInstance.defaultProfileThumbnail,
                        creatorProfile.Value, ct).Forget();
                }
                else
                    donationPanelViewModel.ProfileThumbnail.UpdateValue(ProfileThumbnailViewModel.FromFallback(viewInstance.defaultProfileThumbnail));

                viewInstance!.ConfigureDefaultPanel(donationPanelViewModel);
            }
            catch (OperationCanceledException)
            {
                CloseController();
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.DONATIONS);
                CloseController();
            }
            finally { viewInstance!.SetDefaultLoadingState(false); }
        }

        protected override void OnViewShow() =>
            inputBlock.Disable(BLOCKED_INPUTS);

        protected override void OnViewClose()
        {
            inputBlock.Enable(BLOCKED_INPUTS);
            panelLifecycleCts.SafeCancelAndDispose();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance!.GetClosingTasks(closeIntentCompletionSource.Task, ct));
    }
}
