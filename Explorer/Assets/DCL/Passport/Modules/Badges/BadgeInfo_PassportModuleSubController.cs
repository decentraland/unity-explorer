using Cysharp.Threading.Tasks;
using DCL.BadgesAPIService;
using DCL.Passport.Fields.Badges;
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
        private readonly IObjectPool<BadgeTierButton_PassportFieldView> badgeTierButtonsPool;
        private readonly List<BadgeTierButton_PassportFieldView> instantiatedBadgeTierButtons = new ();

        private bool isOwnProfile;
        private BadgeInfo currentBadgeInfo;
        private CancellationTokenSource loadBadgeTierButtonsCts;

        public BadgeInfo_PassportModuleSubController(
            BadgeInfo_PassportModuleView badgeInfoModuleView,
            IWebRequestController webRequestController)
        {
            this.badgeInfoModuleView = badgeInfoModuleView;

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

        public void Setup(BadgeInfo badgeInfo, bool isOwnProfileBadge)
        {
            this.isOwnProfile = isOwnProfileBadge;
            currentBadgeInfo = badgeInfo;
            badgeInfoModuleView.Setup(badgeInfo, isOwnProfile);
            SetAsLoading(badgeInfo.isTier);

            if (badgeInfo.isTier)
                LoadTierButtons();
        }

        public void SetAsLoading(bool isLoading) =>
            badgeInfoModuleView.SetAsLoading(isLoading);

        public void Clear()
        {
            loadBadgeTierButtonsCts.SafeCancelAndDispose();
            badgeInfoModuleView.StopLoadingImage();

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
            LoadTierButtonsAsync(currentBadgeInfo.id, loadBadgeTierButtonsCts.Token).Forget();
        }

        private async UniTaskVoid LoadTierButtonsAsync(string badgeId, CancellationToken ct)
        {
            // TODO (Santi): This is a placeholder, we should replace this with the real API call
            await UniTask.Delay(1000, cancellationToken: ct);

            foreach (TierData tier in currentBadgeInfo.tiers)
            {
                if (!isOwnProfile && tier.completedAt == null)
                    continue;

                CreateBadgeTierButton(tier);
            }

            SelectLastCompletedTierButton();
            SetAsLoading(false);
        }

        private void CreateBadgeTierButton(TierData tierData)
        {
            var badgeTierButton = badgeTierButtonsPool.Get();
            badgeTierButton.Setup(tierData);
            badgeTierButton.Button.onClick.AddListener(() => SelectTierButton(badgeTierButton));
            instantiatedBadgeTierButtons.Add(badgeTierButton);
        }

        private void SelectLastCompletedTierButton()
        {
            var selectedIndex = 0;
            for (var i = 0; i < instantiatedBadgeTierButtons.Count; i++)
            {
                if (i != currentBadgeInfo.lastCompletedTierIndex)
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
