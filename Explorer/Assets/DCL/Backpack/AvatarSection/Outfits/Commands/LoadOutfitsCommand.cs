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

        public LoadOutfitsCommand(IWebRequestController webRequestController,
            ISelfProfile selfProfile,
            IRealmData realmData)
        {
            this.webRequestController = webRequestController;
            this.selfProfile = selfProfile;
            this.realmData = realmData;
        }

        public async UniTask<IReadOnlyList<OutfitItem>> ExecuteAsync(CancellationToken ct)
        {
            var profile = await selfProfile.ProfileAsync(ct);
            if (profile == null)
            {
                ReportHub.LogError(ReportCategory.OUTFITS, "Cannot get outfits, self profile is not loaded.");
                return Array.Empty<OutfitItem>();
            }

            urlBuilder.Clear();
            urlBuilder.AppendDomain(realmData.Ipfs.LambdasBaseUrl)
                .AppendPath(URLPath.FromString($"outfits/{profile.UserId}"));

            try
            {
                var response = await webRequestController
                    .GetAsync(new CommonArguments(urlBuilder.Build()), ct, ReportCategory.OUTFITS)
                    .CreateFromJson<OutfitsResponse>(WRJsonParser.Newtonsoft);

                var validOutfits = new List<OutfitItem>();

                var outfitsFromServer = response?.Metadata?.outfits;

                if (outfitsFromServer != null)
                {
                    foreach (var outfitItem in outfitsFromServer)
                    {
                        bool isValid = outfitItem.outfit != null && !string.IsNullOrEmpty(outfitItem.outfit.bodyShape);

                        if (isValid)
                        {
                            validOutfits.Add(outfitItem);
                        }
                    }
                }

                ReportHub.Log(ReportCategory.OUTFITS, $"[OUTFIT_LOAD] Loaded {validOutfits.Count} outfits from server.");

                foreach (var outfitItem in validOutfits)
                {
                    if (outfitItem.outfit == null) continue;

                    ReportHub.Log(ReportCategory.OUTFITS, $"[OUTFIT_LOAD]   -> Outfit in Slot {outfitItem.slot} contains {outfitItem.outfit.wearables.Count} wearables:");
                    
                    foreach (string urn in outfitItem.outfit.wearables)
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
                    return Array.Empty<OutfitItem>();

                ReportHub.LogException(e, ReportCategory.OUTFITS);
                return Array.Empty<OutfitItem>();
            }
        }
    }
}