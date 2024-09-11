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
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private readonly IObjectPool<List<LatestAchievedBadgeData>> overviewBadgesPool;

        private readonly BadgesInfo instantiatedDetailedBadges;

        private string badgesBaseUrl => decentralandUrlsSource.Url(DecentralandUrl.Badges);

        public BadgesAPIClient(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;

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

        public async UniTask<BadgesInfo> FetchBadgesAsync(string walletId, bool isOwnProfile, CancellationToken ct)
        {
            ClearDetailedBadges();

            var url = $"{badgesBaseUrl}/users/{walletId}/badges?includeNotAchieved={(isOwnProfile ? "true" : "false")}";

            BadgesResponse badgesResponse = await webRequestController.GetAsync(url, ct, reportCategory: ReportCategory.BADGES)
                                                                      .CreateFromJson<BadgesResponse>(WRJsonParser.Newtonsoft);

            return DetailedBadgesResponseToBadgesInfo(badgesResponse, isOwnProfile);
        }

        public async UniTask<IReadOnlyList<TierData>> FetchTiersAsync(string badgeId, CancellationToken ct)
        {
            var url = $"{badgesBaseUrl}/badges/{badgeId}/tiers";

            TiersResponse tiersResponse = await webRequestController.GetAsync(url, ct, reportCategory: ReportCategory.BADGES)
                                                                    .CreateFromJson<TiersResponse>(WRJsonParser.Newtonsoft);

            return (IReadOnlyList<TierData>)tiersResponse.data.tiers ?? Array.Empty<TierData>();
        }

        private void ClearDetailedBadges()
        {
            instantiatedDetailedBadges.achieved.Clear();
            instantiatedDetailedBadges.notAchieved.Clear();
        }

        private BadgesInfo DetailedBadgesResponseToBadgesInfo(BadgesResponse badgesResponse, bool isOwnProfile)
        {
            foreach (var badge in badgesResponse.data.achieved)
                instantiatedDetailedBadges.achieved.Add(ResponseToBadgeInfo(badge, false, isOwnProfile));

            foreach (var badge in badgesResponse.data.notAchieved)
                instantiatedDetailedBadges.notAchieved.Add(ResponseToBadgeInfo(badge, true, isOwnProfile));

            return instantiatedDetailedBadges;
        }

        private BadgeInfo ResponseToBadgeInfo(BadgeData badge, bool isLocked, bool isOwnProfile)
        {
            var achievedBadgeInfo = new BadgeInfo(badge, isLocked, isOwnProfile && BadgesUtils.IsBadgeNew(badge.id));
            return achievedBadgeInfo;
        }
    }
}
