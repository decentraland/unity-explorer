using Cysharp.Threading.Tasks;
using DCL.BadgesAPIService;
using DCL.Diagnostics;
using DCL.Passport.Fields.Badges;
using DCL.Profiles;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.UI;
using UnityEngine.Pool;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.Passport.Modules.Badges
{
    public class BadgesOverview_PassportModuleController : IPassportModuleController
    {
        private const string ERROR_MESSAGE = "There was an error loading badges. Please try again!";
        private const int BADGES_OVERVIEW_MAX_COUNT = 9;

        private readonly BadgesOverview_PassportModuleView view;
        private readonly BadgesAPIClient badgesAPIClient;
        private readonly PassportErrorsController passportErrorsController;
        private readonly IObjectPool<BadgeOverviewItem_PassportFieldView> badgesOverviewItemsPool;
        private readonly List<BadgeOverviewItem_PassportFieldView> instantiatedBadgesOverviewItems = new ();
        
        private Profile currentProfile;
        private CancellationTokenSource fetchBadgesCts;

        public BadgesOverview_PassportModuleController(
            BadgesOverview_PassportModuleView view,
            BadgesAPIClient badgesAPIClient,
            PassportErrorsController passportErrorsController,
            UITextureProvider textureProvider)
        {
            this.view = view;
            this.badgesAPIClient = badgesAPIClient;
            this.passportErrorsController = passportErrorsController;

            badgesOverviewItemsPool = new ObjectPool<BadgeOverviewItem_PassportFieldView>(
                InstantiateBadgeOverviewItemPrefab,
                defaultCapacity: BADGES_OVERVIEW_MAX_COUNT,
                actionOnGet: badgeOverviewItemView =>
                {
                    badgeOverviewItemView.ConfigureImageController(textureProvider);
                    badgeOverviewItemView.gameObject.SetActive(true);
                    badgeOverviewItemView.gameObject.transform.SetAsLastSibling();
                },
                actionOnRelease: badgeOverviewItemView => badgeOverviewItemView.gameObject.SetActive(false));
        }

        public void Setup(Profile profile)
        {
            currentProfile = profile;
            LoadBadgesOverviewItems();
        }

        public void Clear()
        {
            fetchBadgesCts.SafeCancelAndDispose();

            foreach (BadgeOverviewItem_PassportFieldView badgeOverviewItem in instantiatedBadgesOverviewItems)
            {
                badgeOverviewItem.StopLoadingImage();
                badgesOverviewItemsPool.Release(badgeOverviewItem);
            }

            instantiatedBadgesOverviewItems.Clear();
        }

        public void Dispose() =>
            Clear();

        private BadgeOverviewItem_PassportFieldView InstantiateBadgeOverviewItemPrefab()
        {
            BadgeOverviewItem_PassportFieldView badgeOverviewItemView = Object.Instantiate(view.BadgeOverviewItemPrefab, view.BadgeOverviewItemsContainer);
            return badgeOverviewItemView;
        }

        private void LoadBadgesOverviewItems()
        {
            Clear();

            if (string.IsNullOrEmpty(currentProfile.UserId))
                return;

            fetchBadgesCts = fetchBadgesCts.SafeRestart();
            LoadBadgesOverviewAsync(currentProfile.UserId, fetchBadgesCts.Token).Forget();
        }

        private async UniTaskVoid LoadBadgesOverviewAsync(string walletId, CancellationToken ct)
        {
            try
            {
                var allBadges = await badgesAPIClient.FetchBadgesAsync(walletId, isOwnProfile: false, ct);
                var achievedList = allBadges.achieved;

                achievedList.Sort((a, b) =>
                {
                    long timeA = GetBadgeTimestamp(a);
                    long timeB = GetBadgeTimestamp(b);
                    return timeB.CompareTo(timeA);
                });

                int count = 0;
                foreach (var badgeInfo in achievedList)
                {
                    if (count >= BADGES_OVERVIEW_MAX_COUNT)
                        break;

                    string imageUrl;
                    if (badgeInfo.data.isTier)
                        imageUrl = badgeInfo.data.progress.lastCompletedTierImage ?? string.Empty;
                    else
                        imageUrl = badgeInfo.data.assets?.textures2d?.normal ?? string.Empty;

                    var badgeData = new LatestAchievedBadgeData
                    {
                        id = badgeInfo.data.id, name = badgeInfo.data.name, tierName = badgeInfo.data.progress.lastCompletedTierName, image = imageUrl
                    };

                    var badgeOverviewItem = badgesOverviewItemsPool.Get();
                    badgeOverviewItem.Setup(badgeData);
                    instantiatedBadgesOverviewItems.Add(badgeOverviewItem);

                    count++;
                }

                view.BadgeOverviewItemsContainer.gameObject.SetActive(instantiatedBadgesOverviewItems.Count > 0);
                view.NoBadgesLabel.SetActive(instantiatedBadgesOverviewItems.Count == 0);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                passportErrorsController.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.BADGES, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        // Helper to parse the timestamp safely from either the Tier data or the Badge data
        private long GetBadgeTimestamp(BadgeInfo badge)
        {
            string dateStr;

            if (badge.data.isTier)
                dateStr = badge.data.progress.lastCompletedTierAt;
            else
                dateStr = badge.data.completedAt;

            if (string.IsNullOrEmpty(dateStr))
                return 0;

            if (long.TryParse(dateStr, out long result))
                return result;

            return 0;
        }
    }
}