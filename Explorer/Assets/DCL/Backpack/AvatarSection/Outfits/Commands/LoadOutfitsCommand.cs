using System.Collections.Generic;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Backpack.AvatarSection.Outfits.Logger;
using DCL.Backpack.AvatarSection.Outfits.Models;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Profiles.Self;
using DCL.WebRequests;
using ECS;
using UnityEngine.Pool;

namespace DCL.Backpack.AvatarSection.Outfits.Commands
{
    public class LoadOutfitsCommand
    {
        private readonly IWebRequestController webRequestController;
        private readonly ISelfProfile selfProfile;
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly OutfitsLogger outfitsLogger;

        public LoadOutfitsCommand(IWebRequestController webRequestController,
            ISelfProfile selfProfile,
            IDecentralandUrlsSource urlsSource,
            OutfitsLogger outfitsLogger)
        {
            this.webRequestController = webRequestController;
            this.selfProfile = selfProfile;
            this.urlsSource = urlsSource;
            this.outfitsLogger = outfitsLogger;
        }

        public async UniTask<IReadOnlyDictionary<int, OutfitItem>> ExecuteAsync(CancellationToken ct)
        {
            var profile = await selfProfile.ProfileAsync(ct);
            var empty = new Dictionary<int, OutfitItem>();
            if (profile == null)
            {
                outfitsLogger.LogError("Cannot get outfits, self profile is not loaded.");
                return empty;
            }

            using PooledObject<URLBuilder> _ = urlsSource.BuildFromDomain(DecentralandUrl.Lambdas, out URLBuilder urlBuilder);
            urlBuilder.AppendPath(URLPath.FromString($"outfits/{profile.UserId}"));

            try
            {
                var response = await webRequestController
                    .GetAsync(new CommonArguments(urlBuilder.Build()), ct, ReportCategory.OUTFITS, ignoreErrorCodes: IWebRequestController.IGNORE_NOT_FOUND)
                    .CreateFromJson<OutfitsResponse>(WRJsonParser.Newtonsoft);

                if (response == null)
                {
                    outfitsLogger.LogInfo($"[OUTFIT_LOAD] No outfits found for user {profile?.UserId} (404). This is a normal case for new users.");
                    return empty;
                }

                if (response.Metadata == null)
                {
                    outfitsLogger.LogInfo($"[OUTFIT_LOAD] Loaded old outfits data (version {response.Timestamp}). Ignoring.");
                    return empty;
                }

                var validOutfits = empty;
                var outfitsFromServer = response?.Metadata?.outfits;

                if (outfitsFromServer != null)
                {
                    foreach (var outfitItem in outfitsFromServer)
                    {
                        bool isValid = outfitItem.outfit != null && !string.IsNullOrEmpty(outfitItem.outfit.bodyShape);

                        if (isValid)
                        {
                            validOutfits[outfitItem.slot] = outfitItem;
                        }
                    }
                }

                outfitsLogger.LogLoadResult(validOutfits);
                return validOutfits;
            }
            catch (UnityWebRequestException e)
            {
                ReportHub.LogException(e, ReportCategory.OUTFITS);
                return empty;
            }
        }
    }
}
