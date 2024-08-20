using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.BadgesAPIService
{
    public class BadgesAPIClient
    {
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private string categoriesBaseURL => decentralandUrlsSource.Url(DecentralandUrl.BadgeCategories);
        private string badgesBaseURL => decentralandUrlsSource.Url(DecentralandUrl.Badges);

        public BadgesAPIClient(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public async UniTask<List<string>> FetchBadgeCategoriesAsync(CancellationToken ct)
        {
            CategoriesResponse badgesResponse = await webRequestController.GetAsync(categoriesBaseURL, ct, reportCategory: ReportCategory.BADGES_WEB_REQUEST)
                                                                          .CreateFromJson<CategoriesResponse>(WRJsonParser.Unity);

            return badgesResponse.data;
        }

        public async UniTask<BadgesInfo> FetchBadgesAsync(string walletId, bool includeLockedBadges, int limit, int offset, CancellationToken ct)
        {
            //var url = $"{badgesBaseURL}/{walletId}";

            //BadgesResponse badgesResponse = await webRequestController.GetAsync(url, ct, reportCategory: ReportCategory.BADGES_WEB_REQUEST)
            //                                                          .CreateFromJson<BadgesResponse>(WRJsonParser.Unity);

            //return ResponseToBadgesInfo(badgesResponse);

            await UniTask.Delay(1000, cancellationToken: ct);
            return ResponseToBadgesInfo(GetMockedResponse());
        }

        private BadgesInfo ResponseToBadgesInfo(BadgesResponse badgesResponse)
        {
            BadgesInfo ret = new BadgesInfo
            {
                unlocked = new List<BadgeInfo>(),
                locked = new List<BadgeInfo>(),
            };

            foreach (var badge in badgesResponse.achieved)
                ret.unlocked.Add(ResponseToBadgeInfo(badge, false));

            foreach (var badge in badgesResponse.notAchieved)
                ret.locked.Add(ResponseToBadgeInfo(badge, true));

            return ret;
        }

        private static BadgeInfo ResponseToBadgeInfo(BadgeData badge, bool isLocked)
        {
            int? lastCompletedTierIndex = null;
            for (var i = 0; i < badge.tiers.Length; i++)
            {
                if (badge.progress.stepsDone >= badge.tiers[i].criteria.steps)
                    lastCompletedTierIndex = i;
                else
                    break;
            }

            var nextTierToCompleteIndex = 0;
            for (var i = 0; i < badge.tiers.Length; i++)
            {
                if (badge.progress.stepsTarget != badge.tiers[i].criteria.steps)
                    continue;

                nextTierToCompleteIndex = i;
                break;
            }

            return new BadgeInfo
            {
                id = badge.id,
                isLocked = isLocked,
                category = badge.category,
                name = badge.name,
                description = badge.description,
                image = badge.image,
                completedAt = badge.completedAt,
                isTier = badge.isTier,
                nextTierTotalProgress = badge.progress.stepsTarget ?? 1,
                nextTierCurrentProgress = badge.progress.stepsDone,
                lastCompletedTierIndex = lastCompletedTierIndex,
                nextTierToCompleteIndex = nextTierToCompleteIndex,
                tiers = Array.ConvertAll(badge.tiers, tier => new BadgeTierInfo
                {
                    id = tier.tierId,
                    isLocked = tier.completedAt == null,
                    name = tier.tierName,
                    description = tier.description,
                    awardedAt = tier.completedAt,
                }),
            };
        }

        private static BadgesResponse GetMockedResponse()
        {
            BadgesResponse mockedResponse = new BadgesResponse
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
                            stepsTarget = null,
                        },
                        tiers = Array.Empty<BadgeTierData>(),
                    },
                    new()
                    {
                        id = "emote-creator",
                        completedAt = "1722005503466    ",
                        name = "Emote Creator",
                        description = "50 emotes published",
                        image = "https://images.vexels.com/media/users/3/236713/isolated/preview/2e816f91528e052edec36e8f3e9f52e1-1up-gaming-pixel-art-badge.png?w=360",
                        isTier = true,
                        category = "Socializer",
                        progress = new BadgeProgressData
                        {
                            stepsDone = 50,
                            stepsTarget = 50,
                        },
                        tiers = new[]
                        {
                            new BadgeTierData
                            {
                                tierId = "emote-creator-starter",
                                tierName = "Starter",
                                description = "1 emote published",
                                completedAt = "1722005503466",
                                criteria = new BadgeTierCriteria
                                {
                                    steps = 1,
                                },
                            },
                            new BadgeTierData
                            {
                                tierId = "emote-creator-bronze",
                                tierName = "Bronze",
                                description = "10 emotes published",
                                completedAt = "1722005503466",
                                criteria = new BadgeTierCriteria
                                {
                                    steps = 10,
                                },
                            },
                            new BadgeTierData
                            {
                                tierId = "emote-creator-silver",
                                tierName = "Silver",
                                description = "20 emotes published",
                                completedAt = "1722005503466",
                                criteria = new BadgeTierCriteria
                                {
                                    steps = 20,
                                },
                            },
                            new BadgeTierData
                            {
                                tierId = "emote-creator-gold",
                                tierName = "Gold",
                                description = "30 emotes published",
                                completedAt = "1722005503466",
                                criteria = new BadgeTierCriteria
                                {
                                    steps = 30,
                                },
                            },
                            new BadgeTierData
                            {
                                tierId = "emote-creator-platinum",
                                tierName = "Platinum",
                                description = "40 emotes published",
                                completedAt = "1722005503466",
                                criteria = new BadgeTierCriteria
                                {
                                    steps = 40,
                                },
                            },
                            new BadgeTierData
                            {
                                tierId = "emote-creator-diamond",
                                tierName = "Diamond",
                                description = "50 emotes published",
                                completedAt = "1722005503466",
                                criteria = new BadgeTierCriteria
                                {
                                    steps = 50,
                                },
                            },
                        },
                    },
                    new()
                    {
                        id = "traveler",
                        completedAt = null,
                        name = "Traveler",
                        description = "Visit 60 scenes in Genesis City",
                        image = "https://juststickers.in/wp-content/uploads/2017/06/8-bit-swag-badge.png",
                        isTier = true,
                        category = "Explorer",
                        progress = new BadgeProgressData
                        {
                            stepsDone = 23,
                            stepsTarget = 30,
                        },
                        tiers = new[]
                        {
                            new BadgeTierData
                            {
                                tierId = "traveler-starter",
                                tierName = "Starter",
                                description = "Visit 10 scenes in Genesis City",
                                completedAt = "1722005503466",
                                criteria = new BadgeTierCriteria
                                {
                                    steps = 10,
                                },
                            },
                            new BadgeTierData
                            {
                                tierId = "traveler-bronze",
                                tierName = "Bronze",
                                description = "Visit 20 scenes in Genesis City",
                                completedAt = "1722005503466",
                                criteria = new BadgeTierCriteria
                                {
                                    steps = 20,
                                },
                            },
                            new BadgeTierData
                            {
                                tierId = "traveler-silver",
                                tierName = "Silver",
                                description = "Visit 30 scenes in Genesis City",
                                completedAt = null,
                                criteria = new BadgeTierCriteria
                                {
                                    steps = 30,
                                },
                            },
                            new BadgeTierData
                            {
                                tierId = "traveler-gold",
                                tierName = "Gold",
                                description = "Visit 40 scenes in Genesis City",
                                completedAt = null,
                                criteria = new BadgeTierCriteria
                                {
                                    steps = 40,
                                },
                            },
                            new BadgeTierData
                            {
                                tierId = "traveler-platinum",
                                tierName = "Platinum",
                                description = "Visit 50 scenes in Genesis City",
                                completedAt = null,
                                criteria = new BadgeTierCriteria
                                {
                                    steps = 50,
                                },
                            },
                            new BadgeTierData
                            {
                                tierId = "traveler-diamond",
                                tierName = "Diamond",
                                description = "Visit 60 scenes in Genesis City (Diamond)",
                                completedAt = null,
                                criteria = new BadgeTierCriteria
                                {
                                    steps = 60,
                                },
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
                            stepsDone = 1,
                            stepsTarget = 1,
                        },
                        tiers = Array.Empty<BadgeTierData>(),
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
                            stepsTarget = 1,
                        },
                        tiers = new[]
                        {
                            new BadgeTierData
                            {
                                tierId = "world-jumper-starter",
                                tierName = "Starter",
                                description = "Jump into 1 world",
                                completedAt = null,
                                criteria = new BadgeTierCriteria
                                {
                                    steps = 1,
                                },

                            },
                            new BadgeTierData
                            {
                                tierId = "world-jumper-bronze",
                                tierName = "Bronze",
                                description = "Jump into 2 worlds",
                                completedAt = null,
                                criteria = new BadgeTierCriteria
                                {
                                    steps = 2,
                                },
                            },
                            new BadgeTierData
                            {
                                tierId = "world-jumper-silver",
                                tierName = "Silver",
                                description = "Jump into 3 worlds",
                                completedAt = null,
                                criteria = new BadgeTierCriteria
                                {
                                    steps = 3,
                                },
                            },
                            new BadgeTierData
                            {
                                tierId = "world-jumper-gold",
                                tierName = "Gold",
                                description = "Jump into 4 worlds",
                                completedAt = null,
                                criteria = new BadgeTierCriteria
                                {
                                    steps = 4,
                                },
                            },
                            new BadgeTierData
                            {
                                tierId = "world-jumper-platinum",
                                tierName = "Platinum",
                                description = "Jump into 5 worlds",
                                completedAt = null,
                                criteria = new BadgeTierCriteria
                                {
                                    steps = 5,
                                },
                            },
                            new BadgeTierData
                            {
                                tierId = "world-jumper-diamond",
                                tierName = "Diamond",
                                description = "Jump into 6 worlds",
                                completedAt = null,
                                criteria = new BadgeTierCriteria
                                {
                                    steps = 6,
                                },
                            },
                        },
                    },
                },
            };

            return mockedResponse;
        }
    }
}
