using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Text;
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
            CategoriesResponse badgesResponse = await webRequestController.GetAsync(url, ct, reportCategory: ReportCategory.BADGES_WEB_REQUEST)
                                                                          .CreateFromJson<CategoriesResponse>(WRJsonParser.Unity);

            return badgesResponse.data.categories;
        }

        public async UniTask<List<LatestAchievedBadgeData>> FetchLatestAchievedBadgesAsync(string walletId, CancellationToken ct)
        {
            var url = $"{badgesBaseUrl}/users/{walletId}/preview";

            LatestAchievedBadgesResponse latestAchievedBadgesResponse = await webRequestController.GetAsync(url, ct, reportCategory: ReportCategory.BADGES_WEB_REQUEST)
                                                                                                  .CreateFromJson<LatestAchievedBadgesResponse>(WRJsonParser.Unity);

            //return latestAchievedBadgesResponse.data.latestAchievedBadges;
            return GetLatestAchievedBadgesMockedResponse().data.latestAchievedBadges;
        }

        public async UniTask<BadgesInfo> FetchBadgesAsync(string walletId, bool includeLocked, int limitUnlocked, CancellationToken ct)
        {
            /*StringBuilder url = new StringBuilder($"{badgesBaseUrl}/{walletId}?includeLocked={(includeLocked ? "true" : "false")}");
            if (limitUnlocked > 0)
                url.Append($"&limitUnlocked={limitUnlocked}");

            BadgesResponse badgesResponse = await webRequestController.GetAsync(url.ToString(), ct, reportCategory: ReportCategory.BADGES_WEB_REQUEST)
                                                                      .CreateFromJson<BadgesResponse>(WRJsonParser.Newtonsoft);

            return ResponseToBadgesInfo(badgesResponse);*/

            await UniTask.Delay(1000, cancellationToken: ct);
            return ResponseToBadgesInfo(GetBadgesMockedResponse());
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

        private static BadgeInfo ResponseToBadgeInfo(BadgeData badge, bool isLocked)
        {
            int? lastCompletedTierIndex = null;
            var nextTierToCompleteIndex = 0;
            for (var i = 0; i < badge.tiers.Length; i++)
            {
                if (badge.progress.stepsDone >= badge.tiers[i].criteria.steps)
                    lastCompletedTierIndex = i;

                if (badge.progress.nextStepsTarget == badge.tiers[i].criteria.steps)
                    nextTierToCompleteIndex = i;
            }

            return new BadgeInfo
            {
                id = badge.id,
                name = badge.name,
                description = badge.description,
                category = badge.category,
                isTier = badge.isTier,
                completedAt = badge.completedAt,
                progress = new BadgeProgressData
                {
                    nextStepsTarget = badge.progress.nextStepsTarget,
                    stepsDone = badge.progress.stepsDone,
                    lastCompletedTierAt = badge.progress.lastCompletedTierAt,
                    totalStepsTarget = badge.progress.totalStepsTarget,
                    lastCompletedTierName = badge.progress.lastCompletedTierName,
                    lastCompletedTierImage = badge.progress.lastCompletedTierImage,
                },
                image = badge.image,
                isLocked = isLocked,
                lastCompletedTierIndex = lastCompletedTierIndex,
                nextTierToCompleteIndex = nextTierToCompleteIndex,
                tiers = Array.ConvertAll(badge.tiers, tier => new TierData
                {
                    tierId = tier.tierId,
                    tierName = tier.tierName,
                    description = tier.description,
                    completedAt = tier.completedAt,
                    image = tier.image,
                }),
            };
        }

        // TODO (Santi): Remove these functions when the API is ready
        private static LatestAchievedBadgesResponse GetLatestAchievedBadgesMockedResponse()
        {
            LatestAchievedBadgesResponse mockedResponse = new LatestAchievedBadgesResponse
            {
                data = new LatestAchievedBadgesData
                {
                    latestAchievedBadges = new List<LatestAchievedBadgeData>
                    {
                        new LatestAchievedBadgeData
                        {
                            id = "decentraland-citizen",
                            name = "Decentraland Citizen",
                            image = "https://dejpknyizje2n.cloudfront.net/media/carstickers/versions/pixel-art-golden-medal-award-sticker-u8c98-x450.png",
                        },
                        new LatestAchievedBadgeData
                        {
                            id = "emote-creator",
                            name = "Emote Creator Diamond",
                            image = "https://picsum.photos/seed/6/300/300",
                        },
                        new LatestAchievedBadgeData
                        {
                            id = "traveler",
                            name = "Traveler Bronze",
                            image = "https://picsum.photos/seed/8/300/300",
                        },
                    }
                }
            };

            return mockedResponse;
        }

        private static BadgesResponse GetBadgesMockedResponse()
        {
            BadgesResponse mockedResponse = new BadgesResponse
            {
                data = new ProfileBadgesData
                {
                    achieved = new List<BadgeData>
                    {
                        new()
                        {
                            id = "decentraland-citizen",
                            completedAt = "1722005503466",
                            name = "Decentraland Citizen",
                            description = "Landed in Decentraland",
                            image = "https://dejpknyizje2n.cloudfront.net/media/carstickers/versions/pixel-art-golden-medal-award-sticker-u8c98-x450.png",
                            isTier = false,
                            category = "Explorer",
                            progress = new BadgeProgressData
                            {
                                stepsDone = 1,
                                nextStepsTarget = null,
                                totalStepsTarget = 1,
                                lastCompletedTierAt = null,
                                lastCompletedTierName = null,
                                lastCompletedTierImage = null,
                            },
                            tiers = Array.Empty<TierData>(),
                        },
                        new()
                        {
                            id = "emote-creator",
                            completedAt = "1722005503466",
                            name = "Emote Creator",
                            description = "50 emotes published",
                            image = "https://images.vexels.com/media/users/3/236713/isolated/preview/2e816f91528e052edec36e8f3e9f52e1-1up-gaming-pixel-art-badge.png?w=360",
                            isTier = true,
                            category = "Socializer",
                            progress = new BadgeProgressData
                            {
                                stepsDone = 50,
                                nextStepsTarget = null,
                                totalStepsTarget = 50,
                                lastCompletedTierAt = "1722005503466",
                                lastCompletedTierName = "Diamond",
                                lastCompletedTierImage = "https://picsum.photos/seed/6/300/300",
                            },
                            tiers = new[]
                            {
                                new TierData
                                {
                                    tierId = "emote-creator-starter",
                                    tierName = "Starter",
                                    description = "1 emote published",
                                    completedAt = "1722005503466",
                                    criteria = new BadgeTierCriteria
                                    {
                                        steps = 1,
                                    },
                                    image = "https://picsum.photos/seed/1/300/300",
                                },
                                new TierData
                                {
                                    tierId = "emote-creator-bronze",
                                    tierName = "Bronze",
                                    description = "10 emotes published",
                                    completedAt = "1722005503466",
                                    criteria = new BadgeTierCriteria
                                    {
                                        steps = 10,
                                    },
                                    image = "https://picsum.photos/seed/2/300/300",
                                },
                                new TierData
                                {
                                    tierId = "emote-creator-silver",
                                    tierName = "Silver",
                                    description = "20 emotes published",
                                    completedAt = "1722005503466",
                                    criteria = new BadgeTierCriteria
                                    {
                                        steps = 20,
                                    },
                                    image = "https://picsum.photos/seed/3/300/300",
                                },
                                new TierData
                                {
                                    tierId = "emote-creator-gold",
                                    tierName = "Gold",
                                    description = "30 emotes published",
                                    completedAt = "1722005503466",
                                    criteria = new BadgeTierCriteria
                                    {
                                        steps = 30,
                                    },
                                    image = "https://picsum.photos/seed/4/300/300",
                                },
                                new TierData
                                {
                                    tierId = "emote-creator-platinum",
                                    tierName = "Platinum",
                                    description = "40 emotes published",
                                    completedAt = "1722005503466",
                                    criteria = new BadgeTierCriteria
                                    {
                                        steps = 40,
                                    },
                                    image = "https://picsum.photos/seed/5/300/300",
                                },
                                new TierData
                                {
                                    tierId = "emote-creator-diamond",
                                    tierName = "Diamond",
                                    description = "50 emotes published",
                                    completedAt = "1722005503466",
                                    criteria = new BadgeTierCriteria
                                    {
                                        steps = 50,
                                    },
                                    image = "https://picsum.photos/seed/6/300/300",
                                },
                            },
                        },
                        new()
                        {
                            id = "traveler",
                            completedAt = null,
                            name = "Traveler",
                            description = "Visit 60 scenes in Genesis City",
                            image = "https://art.pixilart.com/2af1a2ad84482b0.png",
                            isTier = true,
                            category = "Explorer",
                            progress = new BadgeProgressData
                            {
                                stepsDone = 23,
                                nextStepsTarget = 30,
                                totalStepsTarget = 60,
                                lastCompletedTierAt = "1722005503466",
                                lastCompletedTierName = "Bronze",
                                lastCompletedTierImage = "https://picsum.photos/seed/8/300/300",
                            },
                            tiers = new[]
                            {
                                new TierData
                                {
                                    tierId = "traveler-starter",
                                    tierName = "Starter",
                                    description = "Visit 10 scenes in Genesis City",
                                    completedAt = "1722005503466",
                                    criteria = new BadgeTierCriteria
                                    {
                                        steps = 10,
                                    },
                                    image = "https://picsum.photos/seed/7/300/300",
                                },
                                new TierData
                                {
                                    tierId = "traveler-bronze",
                                    tierName = "Bronze",
                                    description = "Visit 20 scenes in Genesis City",
                                    completedAt = "1722005503466",
                                    criteria = new BadgeTierCriteria
                                    {
                                        steps = 20,
                                    },
                                    image = "https://picsum.photos/seed/8/300/300",
                                },
                                new TierData
                                {
                                    tierId = "traveler-silver",
                                    tierName = "Silver",
                                    description = "Visit 30 scenes in Genesis City",
                                    completedAt = null,
                                    criteria = new BadgeTierCriteria
                                    {
                                        steps = 30,
                                    },
                                    image = "https://picsum.photos/seed/9/300/300",
                                },
                                new TierData
                                {
                                    tierId = "traveler-gold",
                                    tierName = "Gold",
                                    description = "Visit 40 scenes in Genesis City",
                                    completedAt = null,
                                    criteria = new BadgeTierCriteria
                                    {
                                        steps = 40,
                                    },
                                    image = "https://picsum.photos/seed/10/300/300",
                                },
                                new TierData
                                {
                                    tierId = "traveler-platinum",
                                    tierName = "Platinum",
                                    description = "Visit 50 scenes in Genesis City",
                                    completedAt = null,
                                    criteria = new BadgeTierCriteria
                                    {
                                        steps = 50,
                                    },
                                    image = "https://picsum.photos/seed/11/300/300",
                                },
                                new TierData
                                {
                                    tierId = "traveler-diamond",
                                    tierName = "Diamond",
                                    description = "Visit 60 scenes in Genesis City",
                                    completedAt = null,
                                    criteria = new BadgeTierCriteria
                                    {
                                        steps = 60,
                                    },
                                    image = "https://picsum.photos/seed/12/300/300",
                                },
                            },
                        },
                    },
                    notAchieved = new List<BadgeData>
                    {
                        new()
                        {
                            id = "chat-user",
                            completedAt = null,
                            name = "Chat User",
                            description = "Write something in the chat",
                            image = "https://dejpknyizje2n.cloudfront.net/media/carstickers/versions/pixel-art-golden-trophy-sticker-u3310-x450.png",
                            isTier = false,
                            category = "Socializer",
                            progress = new BadgeProgressData
                            {
                                stepsDone = 0,
                                nextStepsTarget = 1,
                                totalStepsTarget = 1,
                                lastCompletedTierAt = null,
                                lastCompletedTierName = null,
                                lastCompletedTierImage = null,
                            },
                            tiers = Array.Empty<TierData>(),
                        },
                        new()
                        {
                            id = "world-jumper",
                            completedAt = null,
                            name = "World Jumper",
                            description = "Jump into 6 worlds",
                            image = "",
                            isTier = true,
                            category = "Socializer",
                            progress = new BadgeProgressData
                            {
                                stepsDone = 0,
                                nextStepsTarget = 1,
                                totalStepsTarget = 6,
                                lastCompletedTierAt = null,
                                lastCompletedTierName = null,
                                lastCompletedTierImage = null,
                            },
                            tiers = new[]
                            {
                                new TierData
                                {
                                    tierId = "world-jumper-starter",
                                    tierName = "Starter",
                                    description = "Jump into 1 world",
                                    completedAt = null,
                                    criteria = new BadgeTierCriteria
                                    {
                                        steps = 1,
                                    },
                                    image = "",
                                },
                                new TierData
                                {
                                    tierId = "world-jumper-bronze",
                                    tierName = "Bronze",
                                    description = "Jump into 2 worlds",
                                    completedAt = null,
                                    criteria = new BadgeTierCriteria
                                    {
                                        steps = 2,
                                    },
                                    image = "",
                                },
                                new TierData
                                {
                                    tierId = "world-jumper-silver",
                                    tierName = "Silver",
                                    description = "Jump into 3 worlds",
                                    completedAt = null,
                                    criteria = new BadgeTierCriteria
                                    {
                                        steps = 3,
                                    },
                                    image = "",
                                },
                                new TierData
                                {
                                    tierId = "world-jumper-gold",
                                    tierName = "Gold",
                                    description = "Jump into 4 worlds",
                                    completedAt = null,
                                    criteria = new BadgeTierCriteria
                                    {
                                        steps = 4,
                                    },
                                    image = "",
                                },
                                new TierData
                                {
                                    tierId = "world-jumper-platinum",
                                    tierName = "Platinum",
                                    description = "Jump into 5 worlds",
                                    completedAt = null,
                                    criteria = new BadgeTierCriteria
                                    {
                                        steps = 5,
                                    },
                                    image = "",
                                },
                                new TierData
                                {
                                    tierId = "world-jumper-diamond",
                                    tierName = "Diamond",
                                    description = "Jump into 6 worlds",
                                    completedAt = null,
                                    criteria = new BadgeTierCriteria
                                    {
                                        steps = 6,
                                    },
                                    image = "",
                                },
                            },
                        },
                        new()
                        {
                            id = "do-nothing",
                            completedAt = null,
                            name = "Do Nothing",
                            description = "Do nothing during 5 minutes",
                            image = "https://images.vexels.com/content/236707/preview/afk-gaming-pixel-art-badge-b103c0.png",
                            isTier = false,
                            category = "Builder",
                            progress = new BadgeProgressData
                            {
                                stepsDone = 0,
                                nextStepsTarget = 1,
                                totalStepsTarget = 1,
                                lastCompletedTierAt = null,
                                lastCompletedTierName = null,
                                lastCompletedTierImage = null,
                            },
                            tiers = Array.Empty<TierData>(),
                        },
                    },
                }
            };

            return mockedResponse;
        }
    }
}
