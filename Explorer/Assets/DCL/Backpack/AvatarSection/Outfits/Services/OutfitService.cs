using System;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Slots;
using DCL.Profiles.Self;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CommunicationData.URLHelpers;
using DCL.Backpack.AvatarSection.Outfits.Models;
using DCL.Backpack.AvatarSection.Outfits.Repository;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.UI.Profiles.Helpers;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using Newtonsoft.Json;

namespace DCL.Backpack.AvatarSection.Outfits.Services
{
    public class OutfitsService : IOutfitsService
    {
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly ISelfProfile selfProfile;
        private readonly IRealmData realmData;
        private readonly URLBuilder urlBuilder = new ();
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly ProfileRepositoryWrapper profileRepository;
        private readonly OutfitsRepository outfitsRepository;

        private List<OutfitItem> localOutfits = new ();
        private bool outfitsAreDirty  ;

        public OutfitsService(ISelfProfile selfProfile,
            IWebRequestController webRequestController,
            IRealmData realmData,
            OutfitsRepository outfitsRepository)
        {
            this.selfProfile = selfProfile;
            this.webRequestController = webRequestController;
            this.realmData = realmData;
            this.outfitsRepository = outfitsRepository;
        }

        public async UniTask LoadOutfitsAsync(CancellationToken ct)
        {
            var outfitsDict = await GetOutfitsFromServerAsync(ct);

            localOutfits = outfitsDict.Select(kvp => ConvertOutfitDataToOutfitItem(kvp.Key, kvp.Value)).ToList();

            outfitsAreDirty = false;
        }

        public IReadOnlyList<OutfitItem> GetCurrentOutfits()
        {
            return localOutfits;
        }

        public void UpdateLocalOutfit(OutfitItem outfitToSave)
        {
            int existingIndex = localOutfits.FindIndex(o => o.slot == outfitToSave.slot);

            if (existingIndex != -1)
                localOutfits[existingIndex] = outfitToSave;
            else
                localOutfits.Add(outfitToSave);

            outfitsAreDirty = true;
        }

        public void DeleteLocalOutfit(int slotIndex)
        {
            int removedCount = localOutfits.RemoveAll(o => o.slot == slotIndex);
            if (removedCount > 0)
                outfitsAreDirty = true;
        }

        public async UniTask DeployOutfitsIfDirtyAsync(CancellationToken ct)
        {
            if (!outfitsAreDirty) return;

            try
            {
                var profile = await selfProfile.ProfileAsync(ct);
                if (profile != null)
                {
                    string? userId = profile?.UserId;
                    if (userId != null)
                        await outfitsRepository.SetAsync(userId, localOutfits, ct);
                    else
                        ReportHub.LogError(ReportCategory.OUTFITS,
                            "Cannot deploy outfits, self profile has no UserId.");
                }
            }
            catch (Exception ex)
            {
                ReportHub.LogException(ex, $"{ex.Message}");
            }
            finally
            {
                outfitsAreDirty = false;
            }
        }

        private OutfitItem ConvertOutfitDataToOutfitItem(int slot, OutfitData data)
        {
            ReportHub.Log(ReportCategory.OUTFITS,
                $"INVESTIGATION (LOAD): Reading BodyShape URN from OutfitData.BodyShapeUrn: '{data}'");

            return new OutfitItem
            {
                slot = slot, outfit = new Outfit
                {
                    bodyShape = data.BodyShapeUrn, wearables = data.WearableUrns.ToArray(), eyes = new Eyes
                    {
                        color = data.EyesColor
                    },
                    hair = new Hair
                    {
                        color = data.HairColor
                    },
                    skin = new Skin
                    {
                        color = data.SkinColor
                    }
                }
            };
        }

        public async UniTask<Dictionary<int, OutfitData>> GetOutfitsFromServerAsync(CancellationToken ct)
        {
            var profile = await selfProfile.ProfileAsync(ct);
            if (profile == null)
            {
                ReportHub.LogError(ReportCategory.OUTFITS, "Cannot get outfits, self profile is not loaded.");
                return new Dictionary<int, OutfitData>();
            }

            urlBuilder.Clear();
            urlBuilder.AppendDomain(realmData.Ipfs.LambdasBaseUrl)
                .AppendPath(URLPath.FromString($"outfits/{profile.UserId}"));
            var url = urlBuilder.Build();

            try
            {
                var request = webRequestController
                    .GetAsync(new CommonArguments(url), ct, ReportData.UNSPECIFIED);
                var providersDto = await request.CreateFromJson<OutfitsResponse>(WRJsonParser.Newtonsoft);
                var fetchedItems = providersDto.Metadata.outfits;

                var resultDictionary = new Dictionary<int, OutfitData>(fetchedItems.Count);
                foreach (var item in fetchedItems)
                {
                    if (item.outfit == null) continue;

                    resultDictionary[item.slot] = new OutfitData
                    {
                        BodyShapeUrn = item.outfit.bodyShape, WearableUrns = item.outfit.wearables.ToList(), EyesColor = item.outfit.eyes.color, HairColor = item.outfit.hair.color,
                        SkinColor = item.outfit.skin.color, ThumbnailUrl = ""
                    };
                }

                return resultDictionary;
            }
            catch (UnityWebRequestException e)
            {
                if (e.ResponseCode == 404)
                {
                    ReportHub.Log(ReportCategory.OUTFITS, $"No outfits found for user {profile.UserId} (404). Returning empty dictionary.");
                    return new Dictionary<int, OutfitData>();
                }

                ReportHub.LogException(e, ReportCategory.OUTFITS);
                return new Dictionary<int, OutfitData>();
            }
        }

        public async UniTask<bool> ShouldShowExtraOutfitSlotsAsync(CancellationToken ct)
        {
            var profile = await selfProfile.ProfileAsync(ct);
            return profile?.HasClaimedName == true;
        }
    }
}