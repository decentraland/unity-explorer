using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System.Collections.Generic;
using System.Threading;

namespace DCL.BadgesAPIService
{
    public class BadgesAPIClient
    {
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private string badgesBaseUrl => decentralandUrlsSource.Url(DecentralandUrl.Badges);

        public BadgesAPIClient(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public async UniTask<List<string>> FetchBadgeCategoriesAsync(CancellationToken ct)
        {
            var url = $"{badgesBaseUrl}/categories";
            CategoriesResponse badgesResponse = await webRequestController.GetAsync(url, ct, reportCategory: ReportCategory.BADGES)
                                                                          .CreateFromJson<CategoriesResponse>(WRJsonParser.Newtonsoft);

            return badgesResponse.data.categories;
        }

        public async UniTask<List<LatestAchievedBadgeData>> FetchLatestAchievedBadgesAsync(string walletId, CancellationToken ct)
        {
            var url = $"{badgesBaseUrl}/users/{walletId}/preview";

            LatestAchievedBadgesResponse latestAchievedBadgesResponse = await webRequestController.GetAsync(url, ct, reportCategory: ReportCategory.BADGES)
                                                                                                  .CreateFromJson<LatestAchievedBadgesResponse>(WRJsonParser.Newtonsoft);

            return latestAchievedBadgesResponse.data.latestAchievedBadges;
        }

        public async UniTask<BadgesInfo> FetchBadgesAsync(string walletId, bool includeNotAchieved, CancellationToken ct)
        {
            var url = $"{badgesBaseUrl}/users/{walletId}/badges?includeNotAchieved={(includeNotAchieved ? "true" : "false")}";

            BadgesResponse badgesResponse = await webRequestController.GetAsync(url, ct, reportCategory: ReportCategory.BADGES)
                                                                      .CreateFromJson<BadgesResponse>(WRJsonParser.Newtonsoft);

            return ResponseToBadgesInfo(badgesResponse);
        }

        public async UniTask<List<TierData>> FetchTiersAsync(string badgeId, CancellationToken ct)
        {
            var url = $"{badgesBaseUrl}/badges/{badgeId}/tiers";

            TiersResponse tiersResponse = await webRequestController.GetAsync(url, ct, reportCategory: ReportCategory.BADGES)
                                                                    .CreateFromJson<TiersResponse>(WRJsonParser.Newtonsoft);

            return tiersResponse.data.tiers;
        }

        private BadgesInfo ResponseToBadgesInfo(BadgesResponse badgesResponse)
        {
            BadgesInfo badgesInfo = new BadgesInfo
            {
                achieved = new List<BadgeInfo>(),
                notAchieved = new List<BadgeInfo>(),
            };

            foreach (var badge in badgesResponse.data.achieved)
                badgesInfo.achieved.Add(ResponseToBadgeInfo(badge, false));

            foreach (var badge in badgesResponse.data.notAchieved)
                badgesInfo.notAchieved.Add(ResponseToBadgeInfo(badge, true));

            return badgesInfo;
        }

        private static BadgeInfo ResponseToBadgeInfo(BadgeData badge, bool isLocked) =>
            new()
            {
                data = badge,
                isLocked = isLocked,
                isNew = false,
            };
    }
}
