using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.BadgesAPIService;
using DCL.Diagnostics;
using DCL.Passport.Fields.Badges;
using DCL.Passport.Utils;
using DCL.WebRequests;
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

        private readonly BadgeInfo_PassportModuleView badgeInfoModuleView;
        private readonly IWebRequestController webRequestController;
        private readonly BadgesAPIClient badgesAPIClient;
        private readonly PassportErrorsController passportErrorsController;
        private readonly IObjectPool<BadgeTierButton_PassportFieldView> badgeTierButtonsPool;
        private readonly List<BadgeTierButton_PassportFieldView> instantiatedBadgeTierButtons = new ();

        private bool isOwnProfile;
        private BadgeInfo currentBadgeInfo;
        private List<TierData> currentTiers = new ();
        private CancellationTokenSource loadBadgeTierButtonsCts;
        private CancellationTokenSource loadBadge3DImageCts;

        public BadgeInfo_PassportModuleSubController(
            BadgeInfo_PassportModuleView badgeInfoModuleView,
            IWebRequestController webRequestController,
            BadgesAPIClient badgesAPIClient,
            PassportErrorsController passportErrorsController)
        {
            this.badgeInfoModuleView = badgeInfoModuleView;
            this.webRequestController = webRequestController;
            this.badgesAPIClient = badgesAPIClient;
            this.passportErrorsController = passportErrorsController;

            badgeTierButtonsPool = new ObjectPool<BadgeTierButton_PassportFieldView>(
                InstantiateBadgeTierButtonPrefab,
                defaultCapacity: BADGE_TIER_BUTTON_POOL_DEFAULT_CAPACITY,
                actionOnGet: badgeTierButton =>
                {
                    badgeTierButton.ConfigureImageController(webRequestController);
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

            if (badgeInfo.isTier)
                LoadTierButtons();
            else
            {
                SetupBadgeInfoView(badgeInfo, new List<TierData>());
                loadBadge3DImageCts = loadBadge3DImageCts.SafeRestart();
                LoadBadge3DImageAsync(badgeInfo.assets, loadBadge3DImageCts.Token).Forget();
                SetAsLoading(false);
            }
        }

        public void SetAsLoading(bool isLoading)
        {
            badgeInfoModuleView.MainLoadingSpinner.SetActive(isLoading);
            badgeInfoModuleView.MainContainer.SetActive(!isLoading);
        }

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
                List<TierData> tiers = await badgesAPIClient.FetchTiersAsync(badgeInfo.id, ct);

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

        private void SelectLastCompletedTierButton(BadgeInfo badge, List<TierData> tiers)
        {
            int? lastCompletedTierIndex = null;
            for (var i = 0; i < tiers.Count; i++)
            {
                if (badge.progress.stepsDone >= tiers[i].criteria.steps)
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
            badgeInfoModuleView.BadgeNameText.text = $"{badgeInfo.name} {tier.tierName}";
            string tierCompletedAt = badgeInfo.GetTierCompletedDate(tier.tierId);
            badgeInfoModuleView.BadgeDateText.text = !string.IsNullOrEmpty(tierCompletedAt) ? $"Unlocked: {BadgesUtils.FormatTimestampDate(tierCompletedAt)}" : "Locked";
            badgeInfoModuleView.BadgeDescriptionText.text = tier.description;
            badgeInfoModuleView.Badge3DImage.color = string.IsNullOrEmpty(tierCompletedAt) ? badgeInfoModuleView.Badge3DImageLockedColor : badgeInfoModuleView.Badge3DImageUnlockedColor;
            badgeInfoModuleView.Badge3DAnimator.SetBool(IS_STOPPED_3D_IMAGE_ANIMATION_PARAM, string.IsNullOrEmpty(tierCompletedAt));
        }

        private void SetupBadgeInfoView(BadgeInfo badgeInfo, List<TierData> tiers)
        {
            currentTiers = tiers;
            badgeInfoModuleView.TierSection.SetActive(badgeInfo.isTier);
            badgeInfoModuleView.SimpleBadgeProgressBarContainer.SetActive(isOwnProfile && !badgeInfo.isTier && badgeInfo.progress.totalStepsTarget is > 1);
            badgeInfoModuleView.Badge3DImage.color = badgeInfo.isLocked ? badgeInfoModuleView.Badge3DImageLockedColor : badgeInfoModuleView.Badge3DImageUnlockedColor;
            badgeInfoModuleView.Badge3DAnimator.SetBool(IS_STOPPED_3D_IMAGE_ANIMATION_PARAM, badgeInfo.isLocked);

            if (!badgeInfo.isTier)
            {
                badgeInfoModuleView.BadgeNameText.text = badgeInfo.name;
                badgeInfoModuleView.BadgeDateText.text = !badgeInfo.isLocked ? $"Unlocked: {BadgesUtils.FormatTimestampDate(badgeInfo.completedAt)}" : "Locked";
                badgeInfoModuleView.BadgeDescriptionText.text = badgeInfo.description;
                int simpleBadgeProgressPercentage = badgeInfo.progress.stepsDone * 100 / badgeInfo.progress.totalStepsTarget;
                badgeInfoModuleView.SimpleBadgeProgressBarFill.sizeDelta = new Vector2(simpleBadgeProgressPercentage * (badgeInfoModuleView.SimpleBadgeProgressBar.sizeDelta.x / 100), badgeInfoModuleView.SimpleBadgeProgressBarFill.sizeDelta.y);
                badgeInfoModuleView.SimpleBadgeProgressValueText.text = $"{badgeInfo.progress.stepsDone}/{badgeInfo.progress.totalStepsTarget}";
            }
            else
            {
                int nextTierToCompleteIndex = tiers.Count - 1;
                for (var i = 0; i < tiers.Count; i++)
                {
                    if (badgeInfo.progress.nextStepsTarget == tiers[i].criteria.steps)
                        nextTierToCompleteIndex = i;
                }

                var nextTierToComplete = tiers[nextTierToCompleteIndex];
                badgeInfoModuleView.TopTierMark.SetActive(isOwnProfile && !string.IsNullOrEmpty(badgeInfo.completedAt));
                badgeInfoModuleView.NextTierContainer.SetActive(isOwnProfile && string.IsNullOrEmpty(badgeInfo.completedAt) && badgeInfo.progress.stepsDone > 0);
                badgeInfoModuleView.NextTierDescriptionText.gameObject.SetActive(isOwnProfile);
                badgeInfoModuleView.NextTierProgressBarContainer.SetActive(isOwnProfile);

                if (isOwnProfile)
                {
                    badgeInfoModuleView.NextTierValueText.text = nextTierToComplete.tierName;
                    badgeInfoModuleView.NextTierDescriptionText.text = nextTierToComplete.description;
                    int nextTierProgressPercentage = badgeInfo.isLocked ? 0 : badgeInfo.progress.stepsDone * 100 / (badgeInfo.progress.nextStepsTarget ?? badgeInfo.progress.totalStepsTarget);
                    badgeInfoModuleView.NextTierProgressBarFill.sizeDelta = new Vector2((!badgeInfo.isLocked ? nextTierProgressPercentage : 0) * (badgeInfoModuleView.NextTierProgressBar.sizeDelta.x / 100), badgeInfoModuleView.NextTierProgressBarFill.sizeDelta.y);
                    badgeInfoModuleView.NextTierProgressValueText.text = $"{badgeInfo.progress.stepsDone}/{badgeInfo.progress.nextStepsTarget ?? badgeInfo.progress.totalStepsTarget}";
                }
            }
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

                Texture2D baseColorTexture = await webRequestController.GetTextureAsync(new CommonArguments(URLAddress.FromString(baseColorUrl)), new GetTextureArguments(false), GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp), ct);
                baseColorTexture.filterMode = FilterMode.Bilinear;
                Texture2D normalTexture = await webRequestController.GetTextureAsync(new CommonArguments(URLAddress.FromString(normalUrl)), new GetTextureArguments(false), GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp), ct);
                normalTexture.filterMode = FilterMode.Bilinear;
                Texture2D hrmTexture = await webRequestController.GetTextureAsync(new CommonArguments(URLAddress.FromString(hrmUrl)), new GetTextureArguments(false), GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp), ct);
                hrmTexture.filterMode = FilterMode.Bilinear;

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

        private void SetBadgeInfoViewAsLoading(bool isLoading)
        {
            badgeInfoModuleView.ImageLoadingSpinner.SetActive(isLoading);
            badgeInfoModuleView.Badge3DImage.gameObject.SetActive(!isLoading);
        }

        private void Set3DImage(Texture2D baseColor, Texture2D normal, Texture2D hrm)
        {
            badgeInfoModuleView.Badge3DMaterial.SetTexture("_baseColor", baseColor);
            badgeInfoModuleView.Badge3DMaterial.SetTexture("_normal", normal);
            badgeInfoModuleView.Badge3DMaterial.SetTexture("_hrm", hrm);
        }
    }
}