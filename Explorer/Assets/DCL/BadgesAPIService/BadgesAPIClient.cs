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

        private string baseURL => decentralandUrlsSource.Url(DecentralandUrl.Badges);

        public BadgesAPIClient(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public static async UniTask<List<string>> FetchBadgeCategoriesAsync(CancellationToken ct)
        {
            await UniTask.Delay(500, cancellationToken: ct);
            return new List<string> { "Explorer", "Socializer", "Collector", "Creator", "Builder" };
        }

        public async UniTask<List<BadgeInfo>> FetchBadgesAsync(string walletId, bool includeLockedBadges, CancellationToken ct)
        {
            var url = $"{baseURL}/{walletId}";

            BadgesResponse badgesResponse = await webRequestController.GetAsync(url, ct, reportCategory: ReportCategory.BADGES_WEB_REQUEST)
                                                                      .CreateFromJson<BadgesResponse>(WRJsonParser.Unity);

            return ResponseToBadgeInfo(badgesResponse.data);
        }

        private static List<BadgeInfo> ResponseToBadgeInfo(List<BadgeData> response)
        {
            List<BadgeInfo> result = new();

            for (var i = 0; i < response.Count; i++)
            {
                BadgeData badgeResponse = response[i];

                result.Add(new BadgeInfo
                {
                    id = badgeResponse.badge_id,
                    name = badgeResponse.badge_id,
                    description = $"Badge {i + 1} description...",
                    imageUrl = "",
                    date = badgeResponse.awarded_at,
                    isTier = false,
                    isTopTier = false,
                    isLocked = false,
                    progressPercentage = 0,
                    category = "Explorer",
                });
            }

            return result;
        }
    }
}
