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
                totalProgress = badge.totalProgress,
                currentProgress = badge.currentProgress,
                currentTier = badge.currentTier,
                tiers = Array.ConvertAll(badge.tiers, tier => new BadgeTierInfo
                {
                    id = tier.id,
                    isLocked = tier.isLocked,
                    name = tier.name,
                    description = tier.description,
                    image = tier.image,
                    awardedAt = tier.awardedAt,
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
                        totalProgress = 0,
                        currentProgress = 0,
                        currentTier = -1,
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
                        totalProgress = 50,
                        currentProgress = 50,
                        currentTier = 5,
                        tiers = new[]
                        {
                            new BadgeTierData
                            {
                                id = "emote-creator-starter",
                                name = "Emote Creator Starter",
                                description = "Landed in Decentraland (Starter)",
                                image = "",
                                awardedAt = "1722005503466",
                                isLocked = false,
                            },
                            new BadgeTierData
                            {
                                id = "emote-creator-bronze",
                                name = "Emote Creator Bronze",
                                description = "Landed in Decentraland (Bronze)",
                                image = "",
                                awardedAt = "",
                                isLocked = false,
                            },
                            new BadgeTierData
                            {
                                id = "emote-creator-silver",
                                name = "Emote Creator Silver",
                                description = "Landed in Decentraland (Silver)",
                                image = "",
                                awardedAt = "",
                                isLocked = false,
                            },
                            new BadgeTierData
                            {
                                id = "emote-creator-gold",
                                name = "Emote Creator Gold",
                                description = "Landed in Decentraland (Gold)",
                                image = "",
                                awardedAt = "",
                                isLocked = false,
                            },
                            new BadgeTierData
                            {
                                id = "emote-creator-platinum",
                                name = "Emote Creator Platinum",
                                description = "Landed in Decentraland (Platinum)",
                                image = "",
                                awardedAt = "",
                                isLocked = false,
                            },
                            new BadgeTierData
                            {
                                id = "emote-creator-diamond",
                                name = "Emote Creator Diamond",
                                description = "Landed in Decentraland (StarteDiamond)",
                                image = "",
                                awardedAt = "",
                                isLocked = false,
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
                        totalProgress = 10,
                        currentProgress = 2,
                        currentTier = 0,
                        tiers = new[]
                        {
                            new BadgeTierData
                            {
                                id = "traveler-starter",
                                name = "Traveler Starter",
                                description = "Visit 10 scenes in Genesis City (Starter)",
                                image = "",
                                awardedAt = "1722005503466",
                                isLocked = false,
                            },
                            new BadgeTierData
                            {
                                id = "traveler-bronze",
                                name = "Traveler Bronze",
                                description = "Visit 10 scenes in Genesis City (Bronze)",
                                image = "",
                                awardedAt = "",
                                isLocked = false,
                            },
                            new BadgeTierData
                            {
                                id = "traveler-silver",
                                name = "Traveler Silver",
                                description = "Visit 10 scenes in Genesis City (Silver)",
                                image = "",
                                awardedAt = "",
                                isLocked = true,
                            },
                            new BadgeTierData
                            {
                                id = "traveler-gold",
                                name = "Traveler Gold",
                                description = "Visit 10 scenes in Genesis City (Gold)",
                                image = "",
                                awardedAt = "",
                                isLocked = true,
                            },
                            new BadgeTierData
                            {
                                id = "traveler-platinum",
                                name = "Traveler Platinum",
                                description = "Visit 10 scenes in Genesis City (Platinum)",
                                image = "",
                                awardedAt = "",
                                isLocked = true,
                            },
                            new BadgeTierData
                            {
                                id = "traveler-diamond",
                                name = "Traveler Diamond",
                                description = "Visit 10 scenes in Genesis City (Diamond)",
                                image = "",
                                awardedAt = "",
                                isLocked = true,
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
                        totalProgress = 0,
                        currentProgress = 0,
                        currentTier = -1,
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
                        totalProgress = 6,
                        currentProgress = 0,
                        currentTier = -1,
                        tiers = new[]
                        {
                            new BadgeTierData
                            {
                                id = "world-jumper-starter",
                                name = "World Jumper Starter",
                                description = "Jump into 6 worlds (Starter)",
                                image = "",
                                awardedAt = "1722005503466",
                                isLocked = true,
                            },
                            new BadgeTierData
                            {
                                id = "world-jumper-bronze",
                                name = "World Jumper Bronze",
                                description = "Jump into 6 worlds (Bronze)",
                                image = "",
                                awardedAt = "1722005503466",
                                isLocked = true,
                            },
                            new BadgeTierData
                            {
                                id = "world-jumper-silver",
                                name = "World Jumper Silver",
                                description = "Jump into 6 worlds (Silver)",
                                image = "",
                                awardedAt = "1722005503466",
                                isLocked = true,
                            },
                            new BadgeTierData
                            {
                                id = "world-jumper-gold",
                                name = "World Jumper Gold",
                                description = "Jump into 6 worlds (Gold)",
                                image = "",
                                awardedAt = "1722005503466",
                                isLocked = true,
                            },
                            new BadgeTierData
                            {
                                id = "world-jumper-platinum",
                                name = "World Jumper Platinum",
                                description = "Jump into 6 worlds (Platinum)",
                                image = "",
                                awardedAt = "1722005503466",
                                isLocked = true,
                            },
                            new BadgeTierData
                            {
                                id = "world-jumper-diamond",
                                name = "World Jumper Diamond",
                                description = "Jump into 6 worlds (SilveDiamond)",
                                image = "",
                                awardedAt = "1722005503466",
                                isLocked = true,
                            },
                        },
                    },
                },
            };

            return mockedResponse;
        }
    }
}
