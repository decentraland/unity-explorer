using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.BadgesAPIService;
using DCL.Diagnostics;
using DCL.Passport.Fields.Badges;
using DCL.WebRequests;
using DCL.WebRequests.ArgsFactory;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.Passport.Modules.Badges
{
    public class BadgeInfo_PassportModuleSubController
    {
        private const int BADGE_TIER_BUTTON_POOL_DEFAULT_CAPACITY = 6;
        private static readonly int IS_STOPPED_3D_IMAGE_ANIMATION_PARAM = Animator.StringToHash("IsStopped");
        private static readonly int BASE_COLOR = Shader.PropertyToID("_baseColor");
        private static readonly int NORMAL = Shader.PropertyToID("_normal");
        private static readonly int HRM = Shader.PropertyToID("_hrm");

        private readonly BadgeInfo_PassportModuleView badgeInfoModuleView;
        private readonly IWebRequestController webRequestController;
        private readonly IGetTextureArgsFactory getTextureArgsFactory;
        private readonly BadgesAPIClient badgesAPIClient;
        private readonly PassportErrorsController passportErrorsController;
        private readonly IObjectPool<BadgeTierButton_PassportFieldView> badgeTierButtonsPool;
        private readonly List<BadgeTierButton_PassportFieldView> instantiatedBadgeTierButtons = new ();

        private bool isOwnProfile;
        private BadgeInfo currentBadgeInfo;
        private IReadOnlyList<TierData> currentTiers = Array.Empty<TierData>();
        private CancellationTokenSource loadBadgeTierButtonsCts;
        private CancellationTokenSource loadBadge3DImageCts;

        public BadgeInfo_PassportModuleSubController(
            BadgeInfo_PassportModuleView badgeInfoModuleView,
            IWebRequestController webRequestController,
            IGetTextureArgsFactory getTextureArgsFactory,
            BadgesAPIClient badgesAPIClient,
            PassportErrorsController passportErrorsController)
        {
            this.badgeInfoModuleView = badgeInfoModuleView;
            this.webRequestController = webRequestController;
            this.getTextureArgsFactory = getTextureArgsFactory;
            this.badgesAPIClient = badgesAPIClient;
            this.passportErrorsController = passportErrorsController;

            badgeTierButtonsPool = new ObjectPool<BadgeTierButton_PassportFieldView>(
                InstantiateBadgeTierButtonPrefab,
                defaultCapacity: BADGE_TIER_BUTTON_POOL_DEFAULT_CAPACITY,
                actionOnGet: badgeTierButton =>
                {
                    badgeTierButton.ConfigureImageController(webRequestController, getTextureArgsFactory);
                    badgeTierButton.gameObject.SetActive(true);
                    badgeTierButton.SetAsSelected(false);
                    badgeTierButton.transform.SetAsLastSibling();
                },
                actionOnRelease: badgeTierButton => badgeTierButton.gameObject.SetActive(false));
        }

        public void Setup(BadgeInfo badgeInfo, bool isOwnProfileBadge)
        {
            this.isOwnProfile = isOwnProfileBadge;
            currentBadgeInfo = badgeInfo;

            if (badgeInfo.data.isTier)
                LoadTierButtons();
            else
            {
                SetupBadgeInfoView(badgeInfo, Array.Empty<TierData>());
                loadBadge3DImageCts = loadBadge3DImageCts.SafeRestart();
                LoadBadge3DImageAsync(badgeInfo.data.assets, loadBadge3DImageCts.Token).Forget();
                SetAsLoading(false);
            }
        }

        public void SetAsLoading(bool isLoading)
        {
            badgeInfoModuleView.MainLoadingSpinner.SetActive(isLoading);
            badgeInfoModuleView.MainContainer.SetActive(!isLoading);
        }

        public void SetAsEmpty(bool isEmpty) =>
            badgeInfoModuleView.gameObject.SetActive(!isEmpty);

        public void Clear()
        {
            loadBadge3DImageCts.SafeCancelAndDispose();
            ClearTiers();
        }

        private BadgeTierButton_PassportFieldView InstantiateBadgeTierButtonPrefab()
        {
            BadgeTierButton_PassportFieldView badgesFilterButton = Object.Instantiate(badgeInfoModuleView.BadgeTierButtonPrefab, badgeInfoModuleView.AllTiersContainer);
            return badgesFilterButton;
        }

        private void LoadTierButtons()
        {
            Clear();
            SetAsLoading(true);

            loadBadgeTierButtonsCts = loadBadgeTierButtonsCts.SafeRestart();
            LoadTierButtonsAsync(currentBadgeInfo, loadBadgeTierButtonsCts.Token).Forget();
        }

        private async UniTaskVoid LoadTierButtonsAsync(BadgeInfo badgeInfo, CancellationToken ct)
        {
            try
            {
                IReadOnlyList<TierData>? tiers = await badgesAPIClient.FetchTiersAsync(badgeInfo.data.id, ct);

                foreach (TierData tier in tiers)
                {
                    string tierCompletedAt = badgeInfo.GetTierCompletedDate(tier.tierId);

                    if (!isOwnProfile && string.IsNullOrEmpty(tierCompletedAt))
                        continue;

                    CreateBadgeTierButton(tier, tierCompletedAt);
                }

                SetupBadgeInfoView(badgeInfo, tiers);
                SelectLastCompletedTierButton(badgeInfo, tiers);
                SetAsLoading(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error loading tiers. Please try again!";
                passportErrorsController.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.BADGES, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private void CreateBadgeTierButton(TierData tierData, string completedAt)
        {
            var badgeTierButton = badgeTierButtonsPool.Get();
            badgeTierButton.Setup(tierData, completedAt);
            badgeTierButton.Button.onClick.AddListener(() => SelectTierButton(badgeTierButton));
            instantiatedBadgeTierButtons.Add(badgeTierButton);
        }

        private void SelectLastCompletedTierButton(BadgeInfo badge, IReadOnlyList<TierData> tiers)
        {
            int? lastCompletedTierIndex = null;

            for (var i = 0; i < tiers.Count; i++)
            {
                if (badge.data.progress.stepsDone >= tiers[i].criteria.steps)
                    lastCompletedTierIndex = i;
            }

            var selectedIndex = 0;

            for (var i = 0; i < instantiatedBadgeTierButtons.Count; i++)
            {
                if (i != lastCompletedTierIndex)
                    continue;

                selectedIndex = i;
                break;
            }

            if (selectedIndex < instantiatedBadgeTierButtons.Count)
                SelectTierButton(instantiatedBadgeTierButtons[selectedIndex]);
        }

        private void ClearTiers()
        {
            loadBadgeTierButtonsCts.SafeCancelAndDispose();

            foreach (BadgeTierButton_PassportFieldView badgeTierButtons in instantiatedBadgeTierButtons)
            {
                badgeTierButtons.StopLoadingImage();
                badgeTierButtons.Button.onClick.RemoveAllListeners();
                badgeTierButtonsPool.Release(badgeTierButtons);
            }

            instantiatedBadgeTierButtons.Clear();
        }

        private void SelectTierButton(BadgeTierButton_PassportFieldView selectedTierButton)
        {
            for (var i = 0; i < instantiatedBadgeTierButtons.Count; i++)
            {
                BadgeTierButton_PassportFieldView tierButton = instantiatedBadgeTierButtons[i];
                tierButton.SetAsSelected(tierButton.Model.tierId == selectedTierButton.Model.tierId);

                if (tierButton.Model.tierId == selectedTierButton.Model.tierId)
                {
                    SelectBadgeTier(i, currentBadgeInfo);

                    loadBadge3DImageCts = loadBadge3DImageCts.SafeRestart();
                    LoadBadge3DImageAsync(tierButton.Model.assets, loadBadge3DImageCts.Token).Forget();
                }
            }
        }

        private void SelectBadgeTier(int tierIndex, BadgeInfo badgeInfo)
        {
            var tier = currentTiers[tierIndex];
            badgeInfoModuleView.BadgeNameText.text = $"{badgeInfo.data.name} {tier.tierName}";
            string tierCompletedAt = badgeInfo.GetTierCompletedDate(tier.tierId);
            bool tierIsLocked = string.IsNullOrEmpty(tierCompletedAt);
            badgeInfoModuleView.BadgeDateText.text = !tierIsLocked ? $"Unlocked: {BadgesUtils.FormatTimestampDate(tierCompletedAt)}" : "Locked";
            badgeInfoModuleView.BadgeDescriptionText.text = tier.description;
            badgeInfoModuleView.Badge3DImage.color = tierIsLocked ? badgeInfoModuleView.Badge3DImageLockedColor : badgeInfoModuleView.Badge3DImageUnlockedColor;
            badgeInfoModuleView.Badge3DAnimator.SetBool(IS_STOPPED_3D_IMAGE_ANIMATION_PARAM, tierIsLocked);
        }

        private void SetupBadgeInfoView(in BadgeInfo badgeInfo, IReadOnlyList<TierData> tiers)
        {
            currentTiers = tiers;
            badgeInfoModuleView.TierSection.SetActive(badgeInfo.data.isTier);
            badgeInfoModuleView.SimpleBadgeProgressBarContainer.SetActive(isOwnProfile && !badgeInfo.data.isTier && badgeInfo.data.progress.totalStepsTarget > 1);
            badgeInfoModuleView.Badge3DImage.color = badgeInfo.isLocked ? badgeInfoModuleView.Badge3DImageLockedColor : badgeInfoModuleView.Badge3DImageUnlockedColor;
            badgeInfoModuleView.Badge3DAnimator.SetBool(IS_STOPPED_3D_IMAGE_ANIMATION_PARAM, badgeInfo.isLocked);

            if (badgeInfo.data.isTier)
                SetupTierBadge(badgeInfo, tiers);
            else
                SetupNonTierBadge(badgeInfo);
        }

        private void SetupTierBadge(BadgeInfo badgeInfo, IReadOnlyList<TierData> tiers)
        {
            int nextTierToCompleteIndex = tiers.Count - 1;

            for (var i = 0; i < tiers.Count; i++)
            {
                if (badgeInfo.data.progress.nextStepsTarget == tiers[i].criteria.steps)
                    nextTierToCompleteIndex = i;
            }

            var nextTierToComplete = tiers[nextTierToCompleteIndex];
            badgeInfoModuleView.TopTierMark.SetActive(isOwnProfile && !string.IsNullOrEmpty(badgeInfo.data.completedAt));
            badgeInfoModuleView.NextTierContainer.SetActive(isOwnProfile && string.IsNullOrEmpty(badgeInfo.data.completedAt) && badgeInfo.data.progress.stepsDone > 0);
            badgeInfoModuleView.NextTierDescriptionText.gameObject.SetActive(isOwnProfile);
            badgeInfoModuleView.NextTierProgressBarContainer.SetActive(isOwnProfile);

            if (isOwnProfile)
            {
                badgeInfoModuleView.NextTierValueText.text = nextTierToComplete.tierName;
                badgeInfoModuleView.NextTierDescriptionText.text = nextTierToComplete.description;
                int nextTierProgressPercentage = badgeInfo.GetProgressPercentage();
                badgeInfoModuleView.NextTierProgressBarFill.sizeDelta = new Vector2(nextTierProgressPercentage * (badgeInfoModuleView.NextTierProgressBar.sizeDelta.x / 100), badgeInfoModuleView.NextTierProgressBarFill.sizeDelta.y);
                badgeInfoModuleView.NextTierProgressValueText.text = $"{badgeInfo.data.progress.stepsDone}/{badgeInfo.data.progress.nextStepsTarget ?? badgeInfo.data.progress.totalStepsTarget}";
            }
        }

        private void SetupNonTierBadge(BadgeInfo badgeInfo)
        {
            badgeInfoModuleView.BadgeNameText.text = badgeInfo.data.name;
            badgeInfoModuleView.BadgeDateText.text = !badgeInfo.isLocked ? $"Unlocked: {BadgesUtils.FormatTimestampDate(badgeInfo.data.completedAt)}" : "Locked";
            badgeInfoModuleView.BadgeDescriptionText.text = badgeInfo.data.description;
            int simpleBadgeProgressPercentage = badgeInfo.data.progress.stepsDone * 100 / badgeInfo.data.progress.totalStepsTarget;
            badgeInfoModuleView.SimpleBadgeProgressBarFill.sizeDelta = new Vector2(simpleBadgeProgressPercentage * (badgeInfoModuleView.SimpleBadgeProgressBar.sizeDelta.x / 100), badgeInfoModuleView.SimpleBadgeProgressBarFill.sizeDelta.y);
            badgeInfoModuleView.SimpleBadgeProgressValueText.text = $"{badgeInfo.data.progress.stepsDone}/{badgeInfo.data.progress.totalStepsTarget}";
        }

        private async UniTask LoadBadge3DImageAsync(BadgeAssetsData? assets, CancellationToken ct)
        {
            try
            {
                SetBadgeInfoViewAsLoading(true);

                if (assets?.textures3d == null)
                    return;

                string baseColorUrl = assets.textures3d.baseColor;
                string normalUrl = assets.textures3d.normal;
                string hrmUrl = assets.textures3d.hrm;

                Texture2D baseColorTexture = await RemoteTextureAsync(baseColorUrl, ct);
                Texture2D normalTexture = await RemoteTextureAsync(normalUrl, ct);
                Texture2D hrmTexture = await RemoteTextureAsync(hrmUrl, ct);

                Set3DImage(baseColorTexture, normalTexture, hrmTexture);
                SetBadgeInfoViewAsLoading(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error loading badge textures. Please try again!";
                passportErrorsController.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.BADGES, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private async UniTask<Texture2D> RemoteTextureAsync(string url, CancellationToken ct) =>
            (await webRequestController.GetTextureAsync(
                new CommonArguments(URLAddress.FromString(url)),
                getTextureArgsFactory.NewArguments(false),
                GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp, FilterMode.Bilinear),
                ct,
                ReportCategory.BADGES)
            ).Texture;

        private void SetBadgeInfoViewAsLoading(bool isLoading)
        {
            badgeInfoModuleView.ImageLoadingSpinner.SetActive(isLoading);
            badgeInfoModuleView.Badge3DImage.gameObject.SetActive(!isLoading);
        }

        private void Set3DImage(Texture2D baseColor, Texture2D normal, Texture2D hrm)
        {
            badgeInfoModuleView.Badge3DMaterial.SetTexture(BASE_COLOR, baseColor);
            badgeInfoModuleView.Badge3DMaterial.SetTexture(NORMAL, normal);
            badgeInfoModuleView.Badge3DMaterial.SetTexture(HRM, hrm);
        }
    }
}
