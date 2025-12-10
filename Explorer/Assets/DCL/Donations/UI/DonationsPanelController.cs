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
using DCL.UI.Profiles.Helpers;
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
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly decimal[] recommendedDonationAmount;
        private readonly Entity playerEntity;
        private readonly World world;
        private readonly IWebBrowser webBrowser;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IInputBlock inputBlock;

        private CancellationTokenSource panelLifecycleCts = new ();
        private UniTaskCompletionSource closeIntentCompletionSource = new ();
        private Profile? currentCreatorProfile;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public DonationsPanelController(ViewFactoryMethod viewFactory,
            IDonationsService donationsService,
            IProfileRepository profileRepository,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            World world,
            Entity playerEntity,
            IWebBrowser webBrowser,
            IDecentralandUrlsSource decentralandUrlsSource,
            IInputBlock inputBlock,
            decimal[] recommendedDonationAmount)
            : base(viewFactory)
        {
            this.donationsService = donationsService;
            this.profileRepository = profileRepository;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.world = world;
            this.playerEntity = playerEntity;
            this.webBrowser = webBrowser;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.inputBlock = inputBlock;
            this.recommendedDonationAmount = recommendedDonationAmount;
        }

        public override void Dispose()
        {
            panelLifecycleCts.SafeCancelAndDispose();

            if (viewInstance == null) return;

            viewInstance.SendDonationRequested -= OnSendDonationRequestedAsync;
            viewInstance!.BuyMoreRequested -= OnBuyMoreRequested;
            viewInstance!.ContactSupportRequested -= OnContactSupportRequested;
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

        private async void OnSendDonationRequestedAsync(string creatorAddress, decimal amount)
        {
            try
            {
                viewInstance!.ShowLoading(currentCreatorProfile, creatorAddress, amount, profileRepositoryWrapper);

                bool success = await donationsService.SendDonationAsync(creatorAddress, amount, panelLifecycleCts.Token);

                if (success)
                {
                    await viewInstance.ShowTxConfirmedAsync(currentCreatorProfile, creatorAddress, panelLifecycleCts.Token, profileRepositoryWrapper);
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

                (Profile? creatorProfile, decimal currentBalance, decimal manaPriceUsd, string sceneName) =
                    await UniTask.WhenAll(profileRepository.GetAsync(creatorAddress, ct, IProfileRepository.BatchBehaviour.ENFORCE_SINGLE_GET, CatalystRetryPolicy.SIMPLE),
                        donationsService.GetCurrentBalanceAsync(ct),
                        donationsService.GetCurrentManaConversionAsync(ct),
                        donationsService.GetSceneNameAsync(baseParcel, ct));

                currentCreatorProfile = creatorProfile;

                // Scene creators can set a wallet that has nothing to do with DCL, so we can safely log this information to ignore 404s
                if (creatorProfile == null)
                    ReportHub.LogException(new Exception($"Previous 404 on profile {creatorAddress} can be ignored as the wallet might not be stored in catalysts"), ReportCategory.DONATIONS);

                viewInstance!.ConfigureDefaultPanel(creatorProfile, creatorAddress,
                    sceneName, currentBalance,
                    recommendedDonationAmount, manaPriceUsd,
                    profileRepositoryWrapper);
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
