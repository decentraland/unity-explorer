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

        public async UniTask<BadgesInfo> FetchBadgesAsync(string walletId, bool includeLockedBadges, int limit, int offset, CancellationToken ct)
        {
            var url = $"{baseURL}/{walletId}";

            //BadgesResponse badgesResponse = await webRequestController.GetAsync(url, ct, reportCategory: ReportCategory.BADGES_WEB_REQUEST)
            //                                                      .CreateFromJson<BadgesResponse>(WRJsonParser.Unity);

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

            foreach (var badge in badgesResponse.unlocked)
                ret.unlocked.Add(ResponseToBadgeInfo(badge));

            foreach (var badge in badgesResponse.locked)
                ret.locked.Add(ResponseToBadgeInfo(badge));

            return ret;
        }

        private static BadgeInfo ResponseToBadgeInfo(BadgeData badge)
        {
            return new BadgeInfo
            {
                id = badge.id,
                isLocked = badge.isLocked,
                category = badge.category,
                name = badge.name,
                description = badge.description,
                image = badge.image,
                awardedAt = badge.awardedAt,
                isTier = badge.isTier,
                totalStepsToUnlock = badge.totalStepsToUnlock,
                completedSteps = badge.completedSteps,
                tiers = Array.ConvertAll(badge.tiers, tier => new BadgeTierInfo
                {
                    tierId = tier.tierId,
                    tierName = tier.tierName,
                    description = tier.description,
                    image = tier.image,
                    awardedAt = tier.awardedAt,
                    stepsToUnlock = tier.stepsToUnlock,
                }),
            };
        }

        private static BadgesResponse GetMockedResponse()
        {
            BadgesResponse mockedResponse = new BadgesResponse
            {
                unlocked = new List<BadgeData>
                {
                    new()
                    {
                        id = "decentraland-citizen",
                        awardedAt = "1722005503466",
                        name = "Decentraland Citizen",
                        description = "Landed in Decentraland",
                        image = "https://dejpknyizje2n.cloudfront.net/media/carstickers/versions/pixel-art-golden-medal-award-sticker-u8c98-x450.png",
                        isTier = false,
                        isLocked = false,
                        category = "Explorer",
                        totalStepsToUnlock = 0,
                        completedSteps = 0,
                        tiers = Array.Empty<BadgeTierData>(),
                    },
                    new()
                    {
                        id = "emote-creator",
                        awardedAt = "1722005503466",
                        name = "Emote Creator",
                        description = "50 emotes published",
                        image = "https://images.vexels.com/media/users/3/236713/isolated/preview/2e816f91528e052edec36e8f3e9f52e1-1up-gaming-pixel-art-badge.png?w=360",
                        isTier = true,
                        isLocked = false,
                        category = "Socializer",
                        totalStepsToUnlock = 50,
                        completedSteps = 50,
                        tiers = new[]
                        {
                            new BadgeTierData
                            {
                                tierId = "emote-creator-starter",
                                tierName = "Emote Creator Starter",
                                description = "Landed in Decentraland (Starter)",
                                image = "",
                                awardedAt = "1722005503466",
                                stepsToUnlock = 8,
                            },
                            new BadgeTierData
                            {
                                tierId = "emote-creator-bronze",
                                tierName = "Emote Creator Bronze",
                                description = "Landed in Decentraland (Bronze)",
                                image = "",
                                awardedAt = "",
                                stepsToUnlock = 16,
                            },
                            new BadgeTierData
                            {
                                tierId = "emote-creator-silver",
                                tierName = "Emote Creator Silver",
                                description = "Landed in Decentraland (Silver)",
                                image = "",
                                awardedAt = "",
                                stepsToUnlock = 24,
                            },
                            new BadgeTierData
                            {
                                tierId = "emote-creator-gold",
                                tierName = "Emote Creator Gold",
                                description = "Landed in Decentraland (Gold)",
                                image = "",
                                awardedAt = "",
                                stepsToUnlock = 32,
                            },
                            new BadgeTierData
                            {
                                tierId = "emote-creator-platinum",
                                tierName = "Emote Creator Platinum",
                                description = "Landed in Decentraland (Platinum)",
                                image = "",
                                awardedAt = "",
                                stepsToUnlock = 40,
                            },
                            new BadgeTierData
                            {
                                tierId = "emote-creator-diamond",
                                tierName = "Emote Creator Diamond",
                                description = "Landed in Decentraland (StarteDiamond)",
                                image = "",
                                awardedAt = "",
                                stepsToUnlock = 50,
                            },
                        },
                    },
                    new()
                    {
                        id = "traveler",
                        awardedAt = "1722005503466",
                        name = "Traveler",
                        description = "Visit 10 scenes in Genesis City",
                        image = "https://juststickers.in/wp-content/uploads/2017/06/8-bit-swag-badge.png",
                        isTier = true,
                        isLocked = false,
                        category = "Explorer",
                        totalStepsToUnlock = 10,
                        completedSteps = 2,
                        tiers = new[]
                        {
                            new BadgeTierData
                            {
                                tierId = "traveler-starter",
                                tierName = "Traveler Starter",
                                description = "Visit 10 scenes in Genesis City (Starter)",
                                image = "",
                                awardedAt = "1722005503466",
                                stepsToUnlock = 1,
                            },
                            new BadgeTierData
                            {
                                tierId = "traveler-bronze",
                                tierName = "Traveler Bronze",
                                description = "Visit 10 scenes in Genesis City (Bronze)",
                                image = "",
                                awardedAt = "",
                                stepsToUnlock = 2,
                            },
                            new BadgeTierData
                            {
                                tierId = "traveler-silver",
                                tierName = "Traveler Silver",
                                description = "Visit 10 scenes in Genesis City (Silver)",
                                image = "",
                                awardedAt = "",
                                stepsToUnlock = 4,
                            },
                            new BadgeTierData
                            {
                                tierId = "traveler-gold",
                                tierName = "Traveler Gold",
                                description = "Visit 10 scenes in Genesis City (Gold)",
                                image = "",
                                awardedAt = "",
                                stepsToUnlock = 6,
                            },
                            new BadgeTierData
                            {
                                tierId = "traveler-platinum",
                                tierName = "Traveler Platinum",
                                description = "Visit 10 scenes in Genesis City (Platinum)",
                                image = "",
                                awardedAt = "",
                                stepsToUnlock = 8,
                            },
                            new BadgeTierData
                            {
                                tierId = "traveler-diamond",
                                tierName = "Traveler Diamond",
                                description = "Visit 10 scenes in Genesis City (Diamond)",
                                image = "",
                                awardedAt = "",
                                stepsToUnlock = 10,
                            },
                        },
                    },
                },
                locked = new List<BadgeData>
                {
                    new()
                    {
                        id = "chat-user",
                        awardedAt = "1722005503466",
                        name = "Chat User",
                        description = "Write something in the chat",
                        image = "https://dejpknyizje2n.cloudfront.net/media/carstickers/versions/pixel-art-golden-trophy-sticker-u3310-x450.png",
                        isTier = false,
                        isLocked = true,
                        category = "Socializer",
                        totalStepsToUnlock = 0,
                        completedSteps = 0,
                        tiers = Array.Empty<BadgeTierData>(),
                    },
                    new()
                    {
                        id = "world-jumper",
                        awardedAt = "1722005503466",
                        name = "World Jumper",
                        description = "Jump into 6 worlds",
                        image = "",
                        isTier = true,
                        isLocked = true,
                        category = "Socializer",
                        totalStepsToUnlock = 6,
                        completedSteps = 0,
                        tiers = new[]
                        {
                            new BadgeTierData
                            {
                                tierId = "world-jumper-starter",
                                tierName = "World Jumper Starter",
                                description = "Jump into 6 worlds (Starter)",
                                image = "",
                                awardedAt = "1722005503466",
                                stepsToUnlock = 1,
                            },
                            new BadgeTierData
                            {
                                tierId = "world-jumper-bronze",
                                tierName = "World Jumper Bronze",
                                description = "Jump into 6 worlds (Bronze)",
                                image = "",
                                awardedAt = "1722005503466",
                                stepsToUnlock = 2,
                            },
                            new BadgeTierData
                            {
                                tierId = "world-jumper-silver",
                                tierName = "World Jumper Silver",
                                description = "Jump into 6 worlds (Silver)",
                                image = "",
                                awardedAt = "1722005503466",
                                stepsToUnlock = 3,
                            },
                            new BadgeTierData
                            {
                                tierId = "world-jumper-gold",
                                tierName = "World Jumper Gold",
                                description = "Jump into 6 worlds (Gold)",
                                image = "",
                                awardedAt = "1722005503466",
                                stepsToUnlock = 4,
                            },
                            new BadgeTierData
                            {
                                tierId = "world-jumper-platinum",
                                tierName = "World Jumper Platinum",
                                description = "Jump into 6 worlds (Platinum)",
                                image = "",
                                awardedAt = "1722005503466",
                                stepsToUnlock = 5,
                            },
                            new BadgeTierData
                            {
                                tierId = "world-jumper-diamond",
                                tierName = "World Jumper Diamond",
                                description = "Jump into 6 worlds (SilveDiamond)",
                                image = "",
                                awardedAt = "1722005503466",
                                stepsToUnlock = 6,
                            },
                        },
                    },
                },
            };

            return mockedResponse;
        }
    }
}
