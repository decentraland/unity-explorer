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

        public async UniTask<BadgesData> FetchBadgesAsync(string walletId, bool includeLockedBadges, int limit, int offset, CancellationToken ct)
        {
            var url = $"{baseURL}/{walletId}";

            //BadgesData badgesResponse = await webRequestController.GetAsync(url, ct, reportCategory: ReportCategory.BADGES_WEB_REQUEST)
            //                                                      .CreateFromJson<BadgesData>(WRJsonParser.Unity);

            //return badgesResponse;

            await UniTask.Delay(1000, cancellationToken: ct);
            return GetMockedResponse();
        }

        private static BadgesData GetMockedResponse()
        {
            BadgesData mockedData = new BadgesData
            {
                unlocked = new List<BadgeInfo>
                {
                    new()
                    {
                        badge_id = "decentraland-citizen",
                        awarded_at = "1722005503466",
                        name = "Decentraland Citizen",
                        description = "Landed in Decentraland",
                        imageUrl = "https://dejpknyizje2n.cloudfront.net/media/carstickers/versions/pixel-art-golden-medal-award-sticker-u8c98-x450.png",
                        isTier = false,
                        isLocked = false,
                        category = "Explorer",
                        totalStepsToUnlock = 0,
                        completedSteps = 0,
                        tiers = Array.Empty<BadgeTier>(),
                    },
                    new()
                    {
                        badge_id = "emote-creator",
                        awarded_at = "1722005503466",
                        name = "Emote Creator",
                        description = "50 emotes published",
                        imageUrl = "https://images.vexels.com/media/users/3/236713/isolated/preview/2e816f91528e052edec36e8f3e9f52e1-1up-gaming-pixel-art-badge.png?w=360",
                        isTier = true,
                        isLocked = false,
                        category = "Socializer",
                        totalStepsToUnlock = 50,
                        completedSteps = 50,
                        tiers = new[]
                        {
                            new BadgeTier
                            {
                                tier_id = "emote-creator-starter",
                                name = "Emote Creator Starter",
                                previewModelUrl = "",
                                awarded_at = "1722005503466",
                                stepsToUnlock = 8,
                            },
                            new BadgeTier
                            {
                                tier_id = "emote-creator-bronze",
                                name = "Emote Creator Bronze",
                                previewModelUrl = "",
                                awarded_at = "",
                                stepsToUnlock = 16,
                            },
                            new BadgeTier
                            {
                                tier_id = "emote-creator-silver",
                                name = "Emote Creator Silver",
                                previewModelUrl = "",
                                awarded_at = "",
                                stepsToUnlock = 24,
                            },
                            new BadgeTier
                            {
                                tier_id = "emote-creator-gold",
                                name = "Emote Creator Gold",
                                previewModelUrl = "",
                                awarded_at = "",
                                stepsToUnlock = 32,
                            },
                            new BadgeTier
                            {
                                tier_id = "emote-creator-platinum",
                                name = "Emote Creator Platinum",
                                previewModelUrl = "",
                                awarded_at = "",
                                stepsToUnlock = 40,
                            },
                            new BadgeTier
                            {
                                tier_id = "emote-creator-diamond",
                                name = "Emote Creator Diamond",
                                previewModelUrl = "",
                                awarded_at = "",
                                stepsToUnlock = 50,
                            },
                        },
                    },
                    new()
                    {
                        badge_id = "traveler",
                        awarded_at = "1722005503466",
                        name = "Traveler",
                        description = "Visit 10 scenes in Genesis City",
                        imageUrl = "https://juststickers.in/wp-content/uploads/2017/06/8-bit-swag-badge.png",
                        isTier = true,
                        isLocked = false,
                        category = "Explorer",
                        totalStepsToUnlock = 10,
                        completedSteps = 2,
                        tiers = new[]
                        {
                            new BadgeTier
                            {
                                tier_id = "traveler-starter",
                                name = "Traveler Starter",
                                previewModelUrl = "",
                                awarded_at = "1722005503466",
                                stepsToUnlock = 1,
                            },
                            new BadgeTier
                            {
                                tier_id = "traveler-bronze",
                                name = "Traveler Bronze",
                                previewModelUrl = "",
                                awarded_at = "",
                                stepsToUnlock = 2,
                            },
                            new BadgeTier
                            {
                                tier_id = "traveler-silver",
                                name = "Traveler Silver",
                                previewModelUrl = "",
                                awarded_at = "",
                                stepsToUnlock = 4,
                            },
                            new BadgeTier
                            {
                                tier_id = "traveler-gold",
                                name = "Traveler Gold",
                                previewModelUrl = "",
                                awarded_at = "",
                                stepsToUnlock = 6,
                            },
                            new BadgeTier
                            {
                                tier_id = "traveler-platinum",
                                name = "Traveler Platinum",
                                previewModelUrl = "",
                                awarded_at = "",
                                stepsToUnlock = 8,
                            },
                            new BadgeTier
                            {
                                tier_id = "traveler-diamond",
                                name = "Traveler Diamond",
                                previewModelUrl = "",
                                awarded_at = "",
                                stepsToUnlock = 10,
                            },
                        },
                    },
                },
                locked = new List<BadgeInfo>
                {
                    new()
                    {
                        badge_id = "chat-user",
                        awarded_at = "1722005503466",
                        name = "Chat User",
                        description = "Write something in the chat",
                        imageUrl = "https://dejpknyizje2n.cloudfront.net/media/carstickers/versions/pixel-art-golden-trophy-sticker-u3310-x450.png",
                        isTier = false,
                        isLocked = true,
                        category = "Socializer",
                        totalStepsToUnlock = 0,
                        completedSteps = 0,
                        tiers = Array.Empty<BadgeTier>(),
                    },
                    new()
                    {
                        badge_id = "world-jumper",
                        awarded_at = "1722005503466",
                        name = "World Jumper",
                        description = "Jump into 6 worlds",
                        imageUrl = "",
                        isTier = true,
                        isLocked = true,
                        category = "Socializer",
                        totalStepsToUnlock = 6,
                        completedSteps = 0,
                        tiers = new[]
                        {
                            new BadgeTier
                            {
                                tier_id = "world-jumper-starter",
                                name = "World Jumper Starter",
                                previewModelUrl = "",
                                awarded_at = "1722005503466",
                                stepsToUnlock = 1,
                            },
                            new BadgeTier
                            {
                                tier_id = "world-jumper-bronze",
                                name = "World Jumper Bronze",
                                previewModelUrl = "",
                                awarded_at = "1722005503466",
                                stepsToUnlock = 2,
                            },
                            new BadgeTier
                            {
                                tier_id = "world-jumper-silver",
                                name = "World Jumper Silver",
                                previewModelUrl = "",
                                awarded_at = "1722005503466",
                                stepsToUnlock = 3,
                            },
                            new BadgeTier
                            {
                                tier_id = "world-jumper-gold",
                                name = "World Jumper Gold",
                                previewModelUrl = "",
                                awarded_at = "1722005503466",
                                stepsToUnlock = 4,
                            },
                            new BadgeTier
                            {
                                tier_id = "world-jumper-platinum",
                                name = "World Jumper Platinum",
                                previewModelUrl = "",
                                awarded_at = "1722005503466",
                                stepsToUnlock = 5,
                            },
                            new BadgeTier
                            {
                                tier_id = "world-jumper-diamond",
                                name = "World Jumper Diamond",
                                previewModelUrl = "",
                                awarded_at = "1722005503466",
                                stepsToUnlock = 6,
                            },
                        },
                    },
                },
            };

            return mockedData;
        }
    }
}
