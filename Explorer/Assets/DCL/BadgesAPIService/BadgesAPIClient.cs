using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;

namespace DCL.BadgesAPIService
{
    public class BadgesAPIClient
    {
        private const int DETAILED_BADGES_POOL_DEFAULT_CAPACITY = 100;

        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private readonly IObjectPool<List<LatestAchievedBadgeData>> overviewBadgesPool;

        private readonly IObjectPool<BadgeInfo> detailedBadgesPool;
        private readonly BadgesInfo instantiatedDetailedBadges;

        private string badgesBaseUrl => decentralandUrlsSource.Url(DecentralandUrl.Badges);

        public BadgesAPIClient(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;

            detailedBadgesPool = new ObjectPool<BadgeInfo>(
                CreateDetailedBadge,
                defaultCapacity: DETAILED_BADGES_POOL_DEFAULT_CAPACITY,
                actionOnRelease: OnReleaseDetailedBadge);

            instantiatedDetailedBadges = new BadgesInfo
            {
                achieved = new List<BadgeInfo>(),
                notAchieved = new List<BadgeInfo>(),
            };
        }

        public async UniTask<List<string>> FetchBadgeCategoriesAsync(CancellationToken ct)
        {
            var url = $"{badgesBaseUrl}/categories";
            CategoriesResponse badgesResponse = await webRequestController.GetAsync(url, ct, reportCategory: ReportCategory.BADGES)
                                                                          .CreateFromJson<CategoriesResponse>(WRJsonParser.Newtonsoft);

            return badgesResponse.data.categories;
        }

        public async UniTask<IReadOnlyList<LatestAchievedBadgeData>> FetchLatestAchievedBadgesAsync(string walletId, CancellationToken ct)
        {
            var url = $"{badgesBaseUrl}/users/{walletId}/preview";

            LatestAchievedBadgesResponse latestAchievedBadgesResponse = await webRequestController.GetAsync(url, ct, reportCategory: ReportCategory.BADGES)
                                                                                                  .CreateFromJson<LatestAchievedBadgesResponse>(WRJsonParser.Newtonsoft);

            return (IReadOnlyList<LatestAchievedBadgeData>)latestAchievedBadgesResponse.data.latestAchievedBadges ?? Array.Empty<LatestAchievedBadgeData>();
        }

        public async UniTask<BadgesInfo> FetchBadgesAsync(string walletId, bool includeNotAchieved, CancellationToken ct)
        {
            ClearDetailedBadges();

            var url = $"{badgesBaseUrl}/users/{walletId}/badges?includeNotAchieved={(includeNotAchieved ? "true" : "false")}";

            BadgesResponse badgesResponse = await webRequestController.GetAsync(url, ct, reportCategory: ReportCategory.BADGES)
                                                                      .CreateFromJson<BadgesResponse>(WRJsonParser.Newtonsoft);

            return DetailedBadgesResponseToBadgesInfo(badgesResponse);
        }

        public async UniTask<IReadOnlyList<TierData>> FetchTiersAsync(string badgeId, CancellationToken ct)
        {
            var url = $"{badgesBaseUrl}/badges/{badgeId}/tiers";

            TiersResponse tiersResponse = await webRequestController.GetAsync(url, ct, reportCategory: ReportCategory.BADGES)
                                                                    .CreateFromJson<TiersResponse>(WRJsonParser.Newtonsoft);

            return (IReadOnlyList<TierData>)tiersResponse.data.tiers ?? Array.Empty<TierData>();
        }

        private static BadgeInfo CreateDetailedBadge() =>
            new()
            {
                data = new BadgeData
                {
                    assets = new BadgeAssetsData(),
                    progress = new BadgeProgressData { achievedTiers = new List<AchievedTierData>() },
                },
            };

        private static void OnReleaseDetailedBadge(BadgeInfo detailedBadge)
        {
            detailedBadge.data.id = null;
            detailedBadge.data.name = null;
            detailedBadge.data.description = null;
            detailedBadge.data.category = null;
            detailedBadge.data.isTier = false;
            detailedBadge.data.completedAt = null;
            detailedBadge.data.assets.textures2d = null;
            detailedBadge.data.assets.textures3d = null;
            detailedBadge.data.progress.stepsDone = 0;
            detailedBadge.data.progress.nextStepsTarget = null;
            detailedBadge.data.progress.totalStepsTarget = 0;
            detailedBadge.data.progress.lastCompletedTierAt = null;
            detailedBadge.data.progress.lastCompletedTierName = null;
            detailedBadge.data.progress.lastCompletedTierImage = null;
            detailedBadge.data.progress.achievedTiers.Clear();
            detailedBadge.isLocked = false;
            detailedBadge.isNew = false;
        }

        private void ClearDetailedBadges()
        {
            foreach (BadgeInfo achievedBadgeInfo in instantiatedDetailedBadges.achieved)
                detailedBadgesPool.Release(achievedBadgeInfo);

            foreach (BadgeInfo notAchievedBadgeInfo in instantiatedDetailedBadges.notAchieved)
                detailedBadgesPool.Release(notAchievedBadgeInfo);

            instantiatedDetailedBadges.achieved.Clear();
            instantiatedDetailedBadges.notAchieved.Clear();
        }

        private BadgesInfo DetailedBadgesResponseToBadgesInfo(BadgesResponse badgesResponse)
        {
            foreach (var badge in badgesResponse.data.achieved)
                instantiatedDetailedBadges.achieved.Add(ResponseToBadgeInfo(badge, false));

            foreach (var badge in badgesResponse.data.notAchieved)
                instantiatedDetailedBadges.notAchieved.Add(ResponseToBadgeInfo(badge, true));

            return instantiatedDetailedBadges;
        }

        private BadgeInfo ResponseToBadgeInfo(BadgeData badge, bool isLocked)
        {
            BadgeInfo achievedBadgeInfo = detailedBadgesPool.Get();

            achievedBadgeInfo.data.id = badge.id;
            achievedBadgeInfo.data.name = badge.name;
            achievedBadgeInfo.data.description = badge.description;
            achievedBadgeInfo.data.category = badge.category;
            achievedBadgeInfo.data.isTier = badge.isTier;
            achievedBadgeInfo.data.completedAt = badge.completedAt;
            achievedBadgeInfo.data.assets.textures2d = badge.assets.textures2d;
            achievedBadgeInfo.data.assets.textures3d = badge.assets.textures3d;
            achievedBadgeInfo.data.progress.stepsDone = badge.progress.stepsDone;
            achievedBadgeInfo.data.progress.nextStepsTarget = badge.progress.nextStepsTarget;
            achievedBadgeInfo.data.progress.totalStepsTarget = badge.progress.totalStepsTarget;
            achievedBadgeInfo.data.progress.lastCompletedTierAt = badge.progress.lastCompletedTierAt;
            achievedBadgeInfo.data.progress.lastCompletedTierName = badge.progress.lastCompletedTierName;
            achievedBadgeInfo.data.progress.lastCompletedTierImage = badge.progress.lastCompletedTierImage;

            if (badge.progress.achievedTiers != null)
                achievedBadgeInfo.data.progress.achievedTiers.AddRange(badge.progress.achievedTiers);

            achievedBadgeInfo.isLocked = isLocked;
            achievedBadgeInfo.isNew = false;

            return achievedBadgeInfo;
        }
    }
}
