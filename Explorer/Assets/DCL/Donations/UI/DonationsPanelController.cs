using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.Web3;
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

        private readonly DonationsService donationsService;
        private readonly IProfileRepository profileRepository;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly float recommendedDonationAmount;
        private readonly Entity playerEntity;
        private readonly World world;

        private CancellationTokenSource panelLifecycleCts = new ();
        private UniTaskCompletionSource closeIntentCompletionSource = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public DonationsPanelController(ViewFactoryMethod viewFactory,
            DonationsService donationsService,
            IProfileRepository profileRepository,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            World world,
            Entity playerEntity,
            float recommendedDonationAmount)
            : base(viewFactory)
        {
            this.donationsService = donationsService;
            this.profileRepository = profileRepository;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.world = world;
            this.playerEntity = playerEntity;
            this.recommendedDonationAmount = recommendedDonationAmount;
        }

        public override void Dispose()
        {
            panelLifecycleCts.SafeCancelAndDispose();

            if (viewInstance == null) return;

            viewInstance.SendDonationRequested -= OnSendDonationRequested;
        }

        private void CloseController() =>
            closeIntentCompletionSource.TrySetResult();

        protected override void OnViewInstantiated()
        {
            viewInstance!.SendDonationRequested += OnSendDonationRequested;
        }

        protected override void OnBeforeViewShow()
        {
            panelLifecycleCts = panelLifecycleCts.SafeRestart();
            panelLifecycleCts.Token.ThrowIfCancellationRequested();
            closeIntentCompletionSource = new UniTaskCompletionSource();
            LoadDataAsync(panelLifecycleCts.Token).Forget();
        }

        private void OnSendDonationRequested(string creatorAddress, float amount)
        {
            //TODO: Implement donation sending flow
            // async with animation on the main panel
            PlayEmoteByUrn(EMOTE_MONEY_URN);
            CloseController();
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
                viewInstance!.SetLoadingState(true);

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

                Profile? creatorProfile = await profileRepository.GetAsync(creatorAddress, ct, IProfileRepository.BatchBehaviour.ENFORCE_SINGLE_GET, CatalystRetryPolicy.SIMPLE);
                // Scene creators can set a wallet that has nothing to do with DCL, so we can safely log this information to ignore 404s
                if (creatorProfile == null)
                    ReportHub.LogException(new Exception($"Previous 404 on profile {creatorAddress} can be ignored as the wallet might not be stored in catalysts"), ReportCategory.DONATIONS);
                float currentBalance = await donationsService.GetCurrentBalanceAsync(ct);
                float manaPriceUsd = await donationsService.GetCurrentManaConversionAsync(ct);
                string sceneName = await donationsService.GetSceneNameAsync(baseParcel, ct);

                viewInstance!.ConfigurePanel(creatorProfile, creatorAddress,
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
            finally { viewInstance!.SetLoadingState(false); }
        }

        protected override void OnViewClose()
        {
            panelLifecycleCts.SafeCancelAndDispose();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance!.GetClosingTasks(closeIntentCompletionSource.Task, ct));
    }
}
