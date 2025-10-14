using System;
using System.Collections.Generic;
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
                var response = await webRequestController.GetAsync(new CommonArguments(urlBuilder.Build()), ct, ReportData.UNSPECIFIED)
                    .CreateFromJson<OutfitsResponse>(WRJsonParser.Newtonsoft);

                // return response.Metadata.outfits ?? new List<OutfitItem>();

                var loadedOutfits = response.Metadata.outfits ?? new List<OutfitItem>();

                // --- LOGGING POINT 2: What was loaded from the server? ---
                ReportHub.Log(ReportCategory.OUTFITS, $"[OUTFIT_LOAD] Loaded {loadedOutfits.Count} outfits from server.");
                foreach (var outfitItem in loadedOutfits)
                {
                    if (outfitItem.outfit == null) continue;
                    ReportHub.Log(ReportCategory.OUTFITS, $"[OUTFIT_LOAD]   -> Outfit in Slot {outfitItem.slot} contains {outfitItem.outfit.wearables.Length} wearables:");
                    foreach (string urn in outfitItem.outfit.wearables)
                    {
                        ReportHub.Log(ReportCategory.OUTFITS, $"[OUTFIT_LOAD]      -> Wearable URN: '{urn}'");
                    }
                }

                return loadedOutfits;
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