using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Backpack.AvatarSection.Outfits.Models;
using DCL.Diagnostics;
using DCL.Profiles.Self;
using DCL.WebRequests;
using ECS;

namespace DCL.Backpack.AvatarSection.Outfits.Commands
{
    public class LoadOutfitsCommand
    {
        private readonly IWebRequestController webRequestController;
        private readonly ISelfProfile selfProfile;
        private readonly IRealmData realmData;
        private readonly URLBuilder urlBuilder = new ();

        private long migrationTimestamp = new DateTimeOffset(2025, 10, 25, 12, 0, 0, TimeSpan.Zero)
            .ToUnixTimeMilliseconds();

        public LoadOutfitsCommand(IWebRequestController webRequestController,
            ISelfProfile selfProfile,
            IRealmData realmData)
        {
            this.webRequestController = webRequestController;
            this.selfProfile = selfProfile;
            this.realmData = realmData;
        }

        public async UniTask<IReadOnlyDictionary<int, OutfitItem>> ExecuteAsync(CancellationToken ct)
        {
            var profile = await selfProfile.ProfileAsync(ct);
            var empty = new Dictionary<int, OutfitItem>();
            if (profile == null)
            {
                ReportHub.LogError(ReportCategory.OUTFITS, "Cannot get outfits, self profile is not loaded.");
            }

            urlBuilder.Clear();
            urlBuilder.AppendDomain(realmData.Ipfs.LambdasBaseUrl)
                .AppendPath(URLPath.FromString($"outfits/{profile.UserId}"));

            try
            {
                var response = await webRequestController
                    .GetAsync(new CommonArguments(urlBuilder.Build()), ct, ReportCategory.OUTFITS)
                    .CreateFromJson<OutfitsResponse>(WRJsonParser.Newtonsoft);

                if (response.Metadata == null /* || response.Timestamp < migrationTimestamp*/)
                {
                    ReportHub.Log(ReportCategory.OUTFITS, $"[OUTFIT_LOAD] Loaded old outfits data (version {response.Timestamp}). Ignoring.");
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

                ReportHub.Log(ReportCategory.OUTFITS, $"[OUTFIT_LOAD] Loaded {validOutfits.Count} outfits from server.");

                foreach (var outfitItem in validOutfits)
                {
                    if (outfitItem.Value.outfit == null) continue;

                    ReportHub.Log(ReportCategory.OUTFITS, $"[OUTFIT_LOAD]   -> Outfit in Slot {outfitItem.Value.slot} contains {outfitItem.Value.outfit.wearables.Count} wearables:");

                    foreach (string urn in outfitItem.Value.outfit.wearables)
                    {
                        ReportHub.Log(ReportCategory.OUTFITS, $"[OUTFIT_LOAD]      -> Wearable URN: '{urn}'");
                    }
                }

                return validOutfits;
            }
            catch (UnityWebRequestException e)
            {
                // It's common for a user to have no outfits entity,
                // which returns a 404. This is not an error.
                if (e.ResponseCode == 404)
                    return empty;

                ReportHub.LogException(e, ReportCategory.OUTFITS);
                return empty;
            }
        }
    }
}