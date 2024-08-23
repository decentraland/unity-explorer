using Cysharp.Threading.Tasks;
using DCL.BadgesAPIService;
using DCL.Passport.Fields.Badges;
using DCL.Profiles;
using DCL.WebRequests;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.Passport.Modules.Badges
{
    public class BadgeInfo_PassportModuleSubController
    {
        private const int BADGE_TIER_BUTTON_POOL_DEFAULT_CAPACITY = 6;

        private readonly BadgeInfo_PassportModuleView badgeInfoModuleView;
        private readonly BadgesAPIClient badgesAPIClient;
        private readonly IObjectPool<BadgeTierButton_PassportFieldView> badgeTierButtonsPool;
        private readonly List<BadgeTierButton_PassportFieldView> instantiatedBadgeTierButtons = new ();

        private Profile currentProfile;
        private bool isOwnProfile;
        private BadgeInfo currentBadgeInfo;
        private CancellationTokenSource loadBadgeTierButtonsCts;

        public BadgeInfo_PassportModuleSubController(
            BadgeInfo_PassportModuleView badgeInfoModuleView,
            IWebRequestController webRequestController,
            BadgesAPIClient badgesAPIClient)
        {
            this.badgeInfoModuleView = badgeInfoModuleView;
            this.badgesAPIClient = badgesAPIClient;

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

            badgeInfoModuleView.ConfigureImageController(webRequestController);
        }

        public void Setup(BadgeInfo badgeInfo, Profile profile, bool isOwnProfileBadge)
        {
            this.isOwnProfile = isOwnProfileBadge;
            this.currentProfile = profile;
            currentBadgeInfo = badgeInfo;

            if (badgeInfo.isTier)
                LoadTierButtons();
            else
            {
                badgeInfoModuleView.Setup(badgeInfo, new List<TierData>(), isOwnProfile);
                SetAsLoading(false);
            }
        }

        public void SetAsLoading(bool isLoading) =>
            badgeInfoModuleView.SetAsLoading(isLoading);

        public void Clear()
        {
            badgeInfoModuleView.StopLoadingImage();
            ClearTiers();
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
            List<TierData> tiers = await badgesAPIClient.FetchTiersAsync(currentProfile.UserId, badgeInfo.id, ct);

            foreach (TierData tier in tiers)
            {
                if (!isOwnProfile && tier.completedAt == null)
                    continue;

                CreateBadgeTierButton(tier);
            }

            badgeInfoModuleView.Setup(badgeInfo, tiers, isOwnProfile);
            SelectLastCompletedTierButton(badgeInfo, tiers);
            SetAsLoading(false);
        }

        private void CreateBadgeTierButton(TierData tierData)
        {
            var badgeTierButton = badgeTierButtonsPool.Get();
            badgeTierButton.Setup(tierData);
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

        private void SelectTierButton(BadgeTierButton_PassportFieldView selectedTierButton)
        {
            for (var i = 0; i < instantiatedBadgeTierButtons.Count; i++)
            {
                BadgeTierButton_PassportFieldView tierButton = instantiatedBadgeTierButtons[i];
                tierButton.SetAsSelected(tierButton.Model.tierId == selectedTierButton.Model.tierId);

                if (tierButton.Model.tierId == selectedTierButton.Model.tierId)
                    badgeInfoModuleView.SelectBadgeTier(i, currentBadgeInfo);
            }
        }
    }
}
