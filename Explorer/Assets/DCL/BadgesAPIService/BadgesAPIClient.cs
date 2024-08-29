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
            //await UniTask.Delay(1000, cancellationToken: ct);
            //return GetLatestAchievedBadgesMockedResponse().data.latestAchievedBadges;
        }

        public async UniTask<BadgesInfo> FetchBadgesAsync(string walletId, bool includeNotAchieved, CancellationToken ct)
        {
            var url = $"{badgesBaseUrl}/users/{walletId}/badges?includeNotAchieved={(includeNotAchieved ? "true" : "false")}";

            BadgesResponse badgesResponse = await webRequestController.GetAsync(url, ct, reportCategory: ReportCategory.BADGES)
                                                                      .CreateFromJson<BadgesResponse>(WRJsonParser.Newtonsoft);

            return ResponseToBadgesInfo(badgesResponse);
            //await UniTask.Delay(1000, cancellationToken: ct);
            //return ResponseToBadgesInfo(GetBadgesMockedResponse());
        }

        public async UniTask<List<TierData>> FetchTiersAsync(string badgeId, CancellationToken ct)
        {
            var url = $"{badgesBaseUrl}/badges/{badgeId}/tiers";

            TiersResponse tiersResponse = await webRequestController.GetAsync(url, ct, reportCategory: ReportCategory.BADGES)
                                                                    .CreateFromJson<TiersResponse>(WRJsonParser.Newtonsoft);

            return tiersResponse.data.tiers;
            //await UniTask.Delay(1000, cancellationToken: ct);
            //return GetTiersMockedResponse(badgeId).data.tiers;
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
                id = badge.id,
                name = badge.name,
                description = badge.description,
                category = badge.category,
                isTier = badge.isTier,
                completedAt = badge.completedAt,
                assets = badge.assets,
                progress = badge.progress,
                isLocked = isLocked,
                isNew = false,
            };

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
                            image = "https://assets-cdn.decentraland.zone/decentraland-citizen/2d/normal.png",
                        },
                        new LatestAchievedBadgeData
                        {
                            id = "emote-creator",
                            name = "Emote Creator Diamond",
                            image = "https://images.vexels.com/media/users/3/236713/isolated/preview/2e816f91528e052edec36e8f3e9f52e1-1up-gaming-pixel-art-badge.png?w=360",
                        },
                        new LatestAchievedBadgeData
                        {
                            id = "traveler",
                            name = "Traveler Bronze",
                            image = "https://assets-cdn.decentraland.zone/traveler/starter/2d/normal.png",
                        },
                    },
                },
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
                            assets = new BadgeAssetsData
                            {
                                textures2d = new BadgeTexturesData
                                {
                                    normal = "https://assets-cdn.decentraland.zone/decentraland-citizen/2d/normal.png",
                                },
                                textures3d = new BadgeTexturesData
                                {
                                    normal = "https://assets-cdn.decentraland.zone/decentraland-citizen/3d/normal.png",
                                    hrm = "https://assets-cdn.decentraland.zone/decentraland-citizen/3d/hrm.png",
                                    baseColor = "https://assets-cdn.decentraland.zone/decentraland-citizen/3d/basecolor.png",
                                },
                            },
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
                                achievedTiers = null,
                            },
                        },
                        new()
                        {
                            id = "emote-creator",
                            completedAt = "1722005503466",
                            name = "Emote Creator",
                            description = "50 emotes published",
                            assets = new BadgeAssetsData
                            {
                                textures2d = new BadgeTexturesData
                                {
                                    normal = "https://images.vexels.com/media/users/3/236713/isolated/preview/2e816f91528e052edec36e8f3e9f52e1-1up-gaming-pixel-art-badge.png?w=360",
                                },
                                textures3d = new BadgeTexturesData
                                {
                                    normal = "https://i.ibb.co/rMchTDg/Badge03-normal.png",
                                    hrm = "https://i.ibb.co/PgkwB3D/Badge03-hrm.png",
                                    baseColor = "https://i.ibb.co/b15RYpX/Badge03-basecolor.png",
                                },
                            },
                            isTier = true,
                            category = "Socializer",
                            progress = new BadgeProgressData
                            {
                                stepsDone = 50,
                                nextStepsTarget = null,
                                totalStepsTarget = 50,
                                lastCompletedTierAt = "1722005503466",
                                lastCompletedTierName = "Diamond",
                                lastCompletedTierImage = "https://images.vexels.com/media/users/3/236713/isolated/preview/2e816f91528e052edec36e8f3e9f52e1-1up-gaming-pixel-art-badge.png?w=360",
                                achievedTiers = new List<AchievedTierData>
                                {
                                    new()
                                    {
                                        tierId = "emote-creator-starter",
                                        completedAt = "1722005503466",
                                    },
                                    new()
                                    {
                                        tierId = "emote-creator-bronze",
                                        completedAt = "1722005503466",
                                    },
                                    new()
                                    {
                                        tierId = "emote-creator-silver",
                                        completedAt = "1722005503466",
                                    },
                                    new()
                                    {
                                        tierId = "emote-creator-gold",
                                        completedAt = "1722005503466",
                                    },
                                    new()
                                    {
                                        tierId = "emote-creator-platinum",
                                        completedAt = "1722005503466",
                                    },
                                    new()
                                    {
                                        tierId = "emote-creator-diamond",
                                        completedAt = "1722005503466",
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
                            assets = new BadgeAssetsData
                            {
                                textures2d = new BadgeTexturesData
                                {
                                    normal = "https://assets-cdn.decentraland.zone/traveler/starter/2d/normal.png",
                                },
                                textures3d = new BadgeTexturesData
                                {
                                    normal = "https://assets-cdn.decentraland.zone/traveler/starter/3d/normal.png",
                                    hrm = "https://assets-cdn.decentraland.zone/traveler/starter/3d/hrm.png",
                                    baseColor = "https://assets-cdn.decentraland.zone/traveler/starter/3d/basecolor.png",
                                },
                            },
                            isTier = true,
                            category = "Explorer",
                            progress = new BadgeProgressData
                            {
                                stepsDone = 23,
                                nextStepsTarget = 30,
                                totalStepsTarget = 60,
                                lastCompletedTierAt = "1722005503466",
                                lastCompletedTierName = "Bronze",
                                lastCompletedTierImage = "https://assets-cdn.decentraland.zone/traveler/bronze/2d/normal.png",
                                achievedTiers = new List<AchievedTierData>
                                {
                                    new()
                                    {
                                        tierId = "traveler-starter",
                                        completedAt = "1722005503466",
                                    },
                                    new()
                                    {
                                        tierId = "traveler-bronze",
                                        completedAt = "1722005503466",
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
                            assets = new BadgeAssetsData
                            {
                                textures2d = new BadgeTexturesData
                                {
                                    normal = "https://dejpknyizje2n.cloudfront.net/media/carstickers/versions/pixel-art-golden-trophy-sticker-u3310-x450.png",
                                },
                                textures3d = new BadgeTexturesData
                                {
                                    normal = "https://i.ibb.co/rMchTDg/Badge03-normal.png",
                                    hrm = "https://i.ibb.co/PgkwB3D/Badge03-hrm.png",
                                    baseColor = "https://i.ibb.co/b15RYpX/Badge03-basecolor.png",
                                },
                            },
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
                                achievedTiers = null,
                            },
                        },
                        new()
                        {
                            id = "world-jumper",
                            completedAt = null,
                            name = "World Jumper",
                            description = "Jump into 6 worlds",
                            assets = new BadgeAssetsData
                            {
                                textures2d = new BadgeTexturesData
                                {
                                    normal = "https://cdn-icons-png.flaticon.com/512/3760/3760955.png",
                                },
                                textures3d = new BadgeTexturesData
                                {
                                    normal = "https://i.ibb.co/rMchTDg/Badge03-normal.png",
                                    hrm = "https://i.ibb.co/PgkwB3D/Badge03-hrm.png",
                                    baseColor = "https://i.ibb.co/b15RYpX/Badge03-basecolor.png",
                                },
                            },
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
                                achievedTiers = null,
                            },
                        },
                        new()
                        {
                            id = "do-nothing",
                            completedAt = null,
                            name = "Do Nothing",
                            description = "Do nothing during 5 minutes",
                            assets = new BadgeAssetsData
                            {
                                textures2d = new BadgeTexturesData
                                {
                                    normal = "https://images.vexels.com/content/236707/preview/afk-gaming-pixel-art-badge-b103c0.png",
                                },
                                textures3d = new BadgeTexturesData
                                {
                                    normal = "https://i.ibb.co/rMchTDg/Badge03-normal.png",
                                    hrm = "https://i.ibb.co/PgkwB3D/Badge03-hrm.png",
                                    baseColor = "https://i.ibb.co/b15RYpX/Badge03-basecolor.png",
                                },
                            },
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
                                achievedTiers = null,
                            },
                        },
                        new()
                        {
                            id = "completed-store-and-submitted-one-collection",
                            completedAt = null,
                            name = "Open for Business",
                            description = "Complete Store Information and submit at least 1 collection",
                            assets = new BadgeAssetsData
                            {
                                textures2d = new BadgeTexturesData
                                {
                                    normal = "https://www.coywolf.news/wp-content/uploads/2020/07/gmb-badge.png",
                                },
                                textures3d = new BadgeTexturesData
                                {
                                    normal = "https://i.ibb.co/rMchTDg/Badge03-normal.png",
                                    hrm = "https://i.ibb.co/PgkwB3D/Badge03-hrm.png",
                                    baseColor = "https://i.ibb.co/b15RYpX/Badge03-basecolor.png",
                                },
                            },
                            isTier = false,
                            category = "Explorer",
                            progress = new BadgeProgressData
                            {
                                stepsDone = 1,
                                nextStepsTarget = 2,
                                totalStepsTarget = 2,
                                lastCompletedTierAt = null,
                                lastCompletedTierName = null,
                                lastCompletedTierImage = null,
                                achievedTiers = null,
                            },
                        },
                    },
                },
            };

            return mockedResponse;
        }

        private static TiersResponse GetTiersMockedResponse(string badgeId)
        {
            List<TierData> emoteCreatorTiers = new List<TierData>
            {
                new()
                {
                    tierId = "emote-creator-starter",
                    tierName = "Starter",
                    description = "1 emote published",
                    criteria = new BadgeTierCriteria
                    {
                        steps = 1,
                    },
                    assets = new BadgeAssetsData
                    {
                        textures2d = new BadgeTexturesData
                        {
                            normal = "https://images.vexels.com/media/users/3/236713/isolated/preview/2e816f91528e052edec36e8f3e9f52e1-1up-gaming-pixel-art-badge.png?w=360",
                        },
                        textures3d = new BadgeTexturesData
                        {
                            normal = "https://i.ibb.co/rMchTDg/Badge03-normal.png",
                            hrm = "https://i.ibb.co/PgkwB3D/Badge03-hrm.png",
                            baseColor = "https://i.ibb.co/b15RYpX/Badge03-basecolor.png",
                        },
                    },
                },
                new TierData
                {
                    tierId = "emote-creator-bronze",
                    tierName = "Bronze",
                    description = "10 emotes published",
                    criteria = new BadgeTierCriteria
                    {
                        steps = 10,
                    },
                    assets = new BadgeAssetsData
                    {
                        textures2d = new BadgeTexturesData
                        {
                            normal = "https://images.vexels.com/media/users/3/236713/isolated/preview/2e816f91528e052edec36e8f3e9f52e1-1up-gaming-pixel-art-badge.png?w=360",
                        },
                        textures3d = new BadgeTexturesData
                        {
                            normal = "https://i.ibb.co/rMchTDg/Badge03-normal.png",
                            hrm = "https://i.ibb.co/PgkwB3D/Badge03-hrm.png",
                            baseColor = "https://i.ibb.co/b15RYpX/Badge03-basecolor.png",
                        },
                    },
                },
                new TierData
                {
                    tierId = "emote-creator-silver",
                    tierName = "Silver",
                    description = "20 emotes published",
                    criteria = new BadgeTierCriteria
                    {
                        steps = 20,
                    },
                    assets = new BadgeAssetsData
                    {
                        textures2d = new BadgeTexturesData
                        {
                            normal = "https://images.vexels.com/media/users/3/236713/isolated/preview/2e816f91528e052edec36e8f3e9f52e1-1up-gaming-pixel-art-badge.png?w=360",
                        },
                        textures3d = new BadgeTexturesData
                        {
                            normal = "https://i.ibb.co/rMchTDg/Badge03-normal.png",
                            hrm = "https://i.ibb.co/PgkwB3D/Badge03-hrm.png",
                            baseColor = "https://i.ibb.co/b15RYpX/Badge03-basecolor.png",
                        },
                    },
                },
                new TierData
                {
                    tierId = "emote-creator-gold",
                    tierName = "Gold",
                    description = "30 emotes published",
                    criteria = new BadgeTierCriteria
                    {
                        steps = 30,
                    },
                    assets = new BadgeAssetsData
                    {
                        textures2d = new BadgeTexturesData
                        {
                            normal = "https://images.vexels.com/media/users/3/236713/isolated/preview/2e816f91528e052edec36e8f3e9f52e1-1up-gaming-pixel-art-badge.png?w=360",
                        },
                        textures3d = new BadgeTexturesData
                        {
                            normal = "https://i.ibb.co/rMchTDg/Badge03-normal.png",
                            hrm = "https://i.ibb.co/PgkwB3D/Badge03-hrm.png",
                            baseColor = "https://i.ibb.co/b15RYpX/Badge03-basecolor.png",
                        },
                    },
                },
                new TierData
                {
                    tierId = "emote-creator-platinum",
                    tierName = "Platinum",
                    description = "40 emotes published",
                    criteria = new BadgeTierCriteria
                    {
                        steps = 40,
                    },
                    assets = new BadgeAssetsData
                    {
                        textures2d = new BadgeTexturesData
                        {
                            normal = "https://images.vexels.com/media/users/3/236713/isolated/preview/2e816f91528e052edec36e8f3e9f52e1-1up-gaming-pixel-art-badge.png?w=360",
                        },
                        textures3d = new BadgeTexturesData
                        {
                            normal = "https://i.ibb.co/rMchTDg/Badge03-normal.png",
                            hrm = "https://i.ibb.co/PgkwB3D/Badge03-hrm.png",
                            baseColor = "https://i.ibb.co/b15RYpX/Badge03-basecolor.png",
                        },
                    },
                },
                new TierData
                {
                    tierId = "emote-creator-diamond",
                    tierName = "Diamond",
                    description = "50 emotes published",
                    criteria = new BadgeTierCriteria
                    {
                        steps = 50,
                    },
                    assets = new BadgeAssetsData
                    {
                        textures2d = new BadgeTexturesData
                        {
                            normal = "https://images.vexels.com/media/users/3/236713/isolated/preview/2e816f91528e052edec36e8f3e9f52e1-1up-gaming-pixel-art-badge.png?w=360",
                        },
                        textures3d = new BadgeTexturesData
                        {
                            normal = "https://i.ibb.co/rMchTDg/Badge03-normal.png",
                            hrm = "https://i.ibb.co/PgkwB3D/Badge03-hrm.png",
                            baseColor = "https://i.ibb.co/b15RYpX/Badge03-basecolor.png",
                        },
                    },
                },
            };

            List<TierData> travelerTiers = new List<TierData>
            {
                new()
                {
                    tierId = "traveler-starter",
                    tierName = "Starter",
                    description = "Visit 10 scenes in Genesis City",
                    criteria = new BadgeTierCriteria
                    {
                        steps = 10,
                    },
                    assets = new BadgeAssetsData
                    {
                        textures2d = new BadgeTexturesData
                        {
                            normal = "https://assets-cdn.decentraland.zone/traveler/starter/2d/normal.png",
                        },
                        textures3d = new BadgeTexturesData
                        {
                            normal = "https://assets-cdn.decentraland.zone/traveler/starter/3d/normal.png",
                            hrm = "https://assets-cdn.decentraland.zone/traveler/starter/3d/hrm.png",
                            baseColor = "https://assets-cdn.decentraland.zone/traveler/starter/3d/basecolor.png",
                        },
                    },
                },
                new TierData
                {
                    tierId = "traveler-bronze",
                    tierName = "Bronze",
                    description = "Visit 20 scenes in Genesis City",
                    criteria = new BadgeTierCriteria
                    {
                        steps = 20,
                    },
                    assets = new BadgeAssetsData
                    {
                        textures2d = new BadgeTexturesData
                        {
                            normal = "https://assets-cdn.decentraland.zone/traveler/bronze/2d/normal.png",
                        },
                        textures3d = new BadgeTexturesData
                        {
                            normal = "https://assets-cdn.decentraland.zone/traveler/bronze/3d/normal.png",
                            hrm = "https://assets-cdn.decentraland.zone/traveler/bronze/3d/hrm.png",
                            baseColor = "https://assets-cdn.decentraland.zone/traveler/bronze/3d/basecolor.png",
                        },
                    },
                },
                new TierData
                {
                    tierId = "traveler-silver",
                    tierName = "Silver",
                    description = "Visit 30 scenes in Genesis City",
                    criteria = new BadgeTierCriteria
                    {
                        steps = 30,
                    },
                    assets = new BadgeAssetsData
                    {
                        textures2d = new BadgeTexturesData
                        {
                            normal = "https://assets-cdn.decentraland.zone/traveler/silver/2d/normal.png",
                        },
                        textures3d = new BadgeTexturesData
                        {
                            normal = "https://assets-cdn.decentraland.zone/traveler/silver/3d/normal.png",
                            hrm = "https://assets-cdn.decentraland.zone/traveler/silver/3d/hrm.png",
                            baseColor = "https://assets-cdn.decentraland.zone/traveler/silver/3d/basecolor.png",
                        },
                    },
                },
                new TierData
                {
                    tierId = "traveler-gold",
                    tierName = "Gold",
                    description = "Visit 40 scenes in Genesis City",
                    criteria = new BadgeTierCriteria
                    {
                        steps = 40,
                    },
                    assets = new BadgeAssetsData
                    {
                        textures2d = new BadgeTexturesData
                        {
                            normal = "https://assets-cdn.decentraland.zone/traveler/gold/2d/normal.png",
                        },
                        textures3d = new BadgeTexturesData
                        {
                            normal = "https://assets-cdn.decentraland.zone/traveler/gold/3d/normal.png",
                            hrm = "https://assets-cdn.decentraland.zone/traveler/gold/3d/hrm.png",
                            baseColor = "https://assets-cdn.decentraland.zone/traveler/gold/3d/basecolor.png",
                        },
                    },
                },
                new TierData
                {
                    tierId = "traveler-platinum",
                    tierName = "Platinum",
                    description = "Visit 50 scenes in Genesis City",
                    criteria = new BadgeTierCriteria
                    {
                        steps = 50,
                    },
                    assets = new BadgeAssetsData
                    {
                        textures2d = new BadgeTexturesData
                        {
                            normal = "https://assets-cdn.decentraland.zone/traveler/platinum/2d/normal.png",
                        },
                        textures3d = new BadgeTexturesData
                        {
                            normal = "https://assets-cdn.decentraland.zone/traveler/platinum/3d/normal.png",
                            hrm = "https://assets-cdn.decentraland.zone/traveler/platinum/3d/hrm.png",
                            baseColor = "https://assets-cdn.decentraland.zone/traveler/platinum/3d/basecolor.png",
                        },
                    },
                },
                new TierData
                {
                    tierId = "traveler-diamond",
                    tierName = "Diamond",
                    description = "Visit 60 scenes in Genesis City",
                    criteria = new BadgeTierCriteria
                    {
                        steps = 60,
                    },
                    assets = new BadgeAssetsData
                    {
                        textures2d = new BadgeTexturesData
                        {
                            normal = "https://assets-cdn.decentraland.zone/traveler/diamond/2d/normal.png",
                        },
                        textures3d = new BadgeTexturesData
                        {
                            normal = "https://assets-cdn.decentraland.zone/traveler/diamond/3d/normal.png",
                            hrm = "https://assets-cdn.decentraland.zone/traveler/diamond/3d/hrm.png",
                            baseColor = "https://assets-cdn.decentraland.zone/traveler/diamond/3d/basecolor.png",
                        },
                    },
                },
            };

            List<TierData> worldJumperTiers = new List<TierData>
            {
                new()
                {
                    tierId = "world-jumper-starter",
                    tierName = "Starter",
                    description = "Jump into 1 world",
                    criteria = new BadgeTierCriteria
                    {
                        steps = 1,
                    },
                    assets = new BadgeAssetsData
                    {
                        textures2d = new BadgeTexturesData
                        {
                            normal = "https://cdn-icons-png.flaticon.com/512/3760/3760955.png",
                        },
                        textures3d = new BadgeTexturesData
                        {
                            normal = "https://i.ibb.co/rMchTDg/Badge03-normal.png",
                            hrm = "https://i.ibb.co/PgkwB3D/Badge03-hrm.png",
                            baseColor = "https://i.ibb.co/b15RYpX/Badge03-basecolor.png",
                        },
                    },
                },
                new TierData
                {
                    tierId = "world-jumper-bronze",
                    tierName = "Bronze",
                    description = "Jump into 2 worlds",
                    criteria = new BadgeTierCriteria
                    {
                        steps = 2,
                    },
                    assets = new BadgeAssetsData
                    {
                        textures2d = new BadgeTexturesData
                        {
                            normal = "https://cdn-icons-png.flaticon.com/512/3760/3760955.png",
                        },
                        textures3d = new BadgeTexturesData
                        {
                            normal = "https://i.ibb.co/rMchTDg/Badge03-normal.png",
                            hrm = "https://i.ibb.co/PgkwB3D/Badge03-hrm.png",
                            baseColor = "https://i.ibb.co/b15RYpX/Badge03-basecolor.png",
                        },
                    },
                },
                new TierData
                {
                    tierId = "world-jumper-silver",
                    tierName = "Silver",
                    description = "Jump into 3 worlds",
                    criteria = new BadgeTierCriteria
                    {
                        steps = 3,
                    },
                    assets = new BadgeAssetsData
                    {
                        textures2d = new BadgeTexturesData
                        {
                            normal = "https://cdn-icons-png.flaticon.com/512/3760/3760955.png",
                        },
                        textures3d = new BadgeTexturesData
                        {
                            normal = "https://i.ibb.co/rMchTDg/Badge03-normal.png",
                            hrm = "https://i.ibb.co/PgkwB3D/Badge03-hrm.png",
                            baseColor = "https://i.ibb.co/b15RYpX/Badge03-basecolor.png",
                        },
                    },
                },
                new TierData
                {
                    tierId = "world-jumper-gold",
                    tierName = "Gold",
                    description = "Jump into 4 worlds",
                    criteria = new BadgeTierCriteria
                    {
                        steps = 4,
                    },
                    assets = new BadgeAssetsData
                    {
                        textures2d = new BadgeTexturesData
                        {
                            normal = "https://cdn-icons-png.flaticon.com/512/3760/3760955.png",
                        },
                        textures3d = new BadgeTexturesData
                        {
                            normal = "https://i.ibb.co/rMchTDg/Badge03-normal.png",
                            hrm = "https://i.ibb.co/PgkwB3D/Badge03-hrm.png",
                            baseColor = "https://i.ibb.co/b15RYpX/Badge03-basecolor.png",
                        },
                    },
                },
                new TierData
                {
                    tierId = "world-jumper-platinum",
                    tierName = "Platinum",
                    description = "Jump into 5 worlds",
                    criteria = new BadgeTierCriteria
                    {
                        steps = 5,
                    },
                    assets = new BadgeAssetsData
                    {
                        textures2d = new BadgeTexturesData
                        {
                            normal = "https://cdn-icons-png.flaticon.com/512/3760/3760955.png",
                        },
                        textures3d = new BadgeTexturesData
                        {
                            normal = "https://i.ibb.co/rMchTDg/Badge03-normal.png",
                            hrm = "https://i.ibb.co/PgkwB3D/Badge03-hrm.png",
                            baseColor = "https://i.ibb.co/b15RYpX/Badge03-basecolor.png",
                        },
                    },
                },
                new TierData
                {
                    tierId = "world-jumper-diamond",
                    tierName = "Diamond",
                    description = "Jump into 6 worlds",
                    criteria = new BadgeTierCriteria
                    {
                        steps = 6,
                    },
                    assets = new BadgeAssetsData
                    {
                        textures2d = new BadgeTexturesData
                        {
                            normal = "https://cdn-icons-png.flaticon.com/512/3760/3760955.png",
                        },
                        textures3d = new BadgeTexturesData
                        {
                            normal = "https://i.ibb.co/rMchTDg/Badge03-normal.png",
                            hrm = "https://i.ibb.co/PgkwB3D/Badge03-hrm.png",
                            baseColor = "https://i.ibb.co/b15RYpX/Badge03-basecolor.png",
                        },
                    },
                },
            };

            List<TierData> tiersResult = new List<TierData>();
            switch (badgeId)
            {
                case "emote-creator":
                    tiersResult = emoteCreatorTiers;
                    break;
                case "traveler":
                    tiersResult = travelerTiers;
                    break;
                case "world-jumper":
                    tiersResult = worldJumperTiers;
                    break;
            }

            TiersResponse tiersResponse = new TiersResponse { data = new TiersData { tiers = tiersResult } };

            return tiersResponse;
        }
    }
}
