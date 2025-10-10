using System;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Slots;
using DCL.Profiles.Self;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.AvatarSection.Outfits.Models;
using DCL.Backpack.AvatarSection.Outfits.Repository;
using DCL.Backpack.Outfits.Extensions;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.UI.Profiles.Helpers;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using Runtime.Wearables;

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
        private readonly IWearableStorage wearableStorage;
        private readonly IAvatarScreenshotService avatarScreenshotService;

        private List<OutfitItem> localOutfits = new ();
        private bool outfitsAreDirty  ;

        public OutfitsService(ISelfProfile selfProfile,
            IWebRequestController webRequestController,
            IRealmData realmData,
            OutfitsRepository outfitsRepository,
            IWearableStorage wearableStorage,
            IAvatarScreenshotService avatarScreenshotService)
        {
            this.selfProfile = selfProfile;
            this.webRequestController = webRequestController;
            this.realmData = realmData;
            this.outfitsRepository = outfitsRepository;
            this.wearableStorage = wearableStorage;
            this.avatarScreenshotService = avatarScreenshotService;
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

        private async UniTask CreateAndUpdateLocalOutfitAsync(int slotIndex, IEquippedWearables equippedWearables, Action<OutfitItem> onUpdateOutfit)
        {
            var profile = await selfProfile.ProfileAsync(CancellationToken.None);
            if (profile == null)
            {
                ReportHub.LogError(ReportCategory.OUTFITS, "Cannot create outfit, self profile is not loaded.");
                return;
            }

            // Get the list of fully-qualified "Item URNs" using the correct extension method.
            List<string> fullItemUrns = equippedWearables.ToFullWearableUrns(wearableStorage, profile);

            // 2. Get the other live data from IEquippedWearables.
            var (hairColor, eyesColor, skinColor) = equippedWearables.GetColors();
            if (!equippedWearables.Items().TryGetValue(WearableCategories.Categories.BODY_SHAPE, out var bodyShapeWearable) || bodyShapeWearable == null)
            {
                ReportHub.LogError(ReportCategory.OUTFITS, "Cannot save outfit, Body Shape is not equipped!");
                return;
            }

            string liveBodyShapeUrn = bodyShapeWearable.GetUrn();

            // Build the final, correct OutfitItem.
            var newItem = new OutfitItem
            {
                slot = slotIndex, outfit = new Outfit
                {
                    bodyShape = liveBodyShapeUrn, wearables = fullItemUrns.ToArray(), eyes = new Eyes
                    {
                        color = eyesColor
                    },
                    hair = new Hair
                    {
                        color = hairColor
                    },
                    skin = new Skin
                    {
                        color = skinColor
                    }
                }
            };

            // Update the internal state (this is the same as before).
            UpdateLocalOutfit(newItem);

            // Invoke the callback to notify the caller.
            onUpdateOutfit?.Invoke(newItem);
        }

        public void CreateAndUpdateLocalOutfit(int slotIndex, IEquippedWearables equippedWearables, Action<OutfitItem> onUpdateOutfit)
        {
            CreateAndUpdateLocalOutfitAsync(slotIndex, equippedWearables, onUpdateOutfit).Forget();
        }

        public async UniTask<OutfitItem?> CreateAndSaveOutfitToServerAsync(int slotIndex, IEquippedWearables equippedWearables, CancellationToken ct)
        {
            var profile = await selfProfile.ProfileAsync(CancellationToken.None);
            if (profile == null)
            {
                ReportHub.LogError(ReportCategory.OUTFITS, "Cannot create outfit, self profile is not loaded.");
                return null;
            }

            List<string> fullItemUrns = equippedWearables.ToFullWearableUrns(wearableStorage, profile);

            var (hairColor, eyesColor, skinColor) = equippedWearables.GetColors();
            if (!equippedWearables.Items().TryGetValue(WearableCategories.Categories.BODY_SHAPE, out var bodyShapeWearable)
                || bodyShapeWearable == null)
            {
                ReportHub.LogError(ReportCategory.OUTFITS, "Cannot save outfit, Body Shape is not equipped!");
                return null;
            }

            string liveBodyShapeUrn = bodyShapeWearable.GetUrn();

            // Build the final, correct OutfitItem.
            var newItem = new OutfitItem
            {
                slot = slotIndex, outfit = new Outfit
                {
                    bodyShape = liveBodyShapeUrn, wearables = fullItemUrns.ToArray(), eyes = new Eyes
                    {
                        color = eyesColor
                    },
                    hair = new Hair
                    {
                        color = hairColor
                    },
                    skin = new Skin
                    {
                        color = skinColor
                    }
                }
            };

            // 2. Update the local in-memory list immediately
            UpdateLocalOutfit(newItem);

            try
            {
                // The repository saves the *entire collection* of outfits, which is what we want.
                await outfitsRepository.SetAsync(profile.UserId, localOutfits, ct);
                outfitsAreDirty = false; // The save was successful, so the state is no longer dirty.
                return newItem; // Return the new item on success
            }
            catch (Exception ex)
            {
                ReportHub.LogException(ex, "Failed to deploy outfit immediately.");
                // Optional: You could revert the local change here if the save fails.
                return null;
            }
        }

        public async UniTask<bool> DeleteOutfitFromServerAsync(int slotIndex, CancellationToken ct)
        {
            var profile = await selfProfile.ProfileAsync(ct);
            if (profile == null)
            {
                ReportHub.LogError(ReportCategory.OUTFITS, "Cannot delete outfit, self profile is not loaded.");
                return false;
            }

            var outfitToRemove = localOutfits.FirstOrDefault(o => o.slot == slotIndex);
            if (outfitToRemove == null) return true;
            int originalIndex = localOutfits.IndexOf(outfitToRemove);
            localOutfits.Remove(outfitToRemove);

            try
            {
                // 1. Deploy the metadata change to the server FIRST.
                // This is the operation most likely to fail due to network issues.
                await outfitsRepository.SetAsync(profile.UserId, localOutfits, ct);

                // 2. If the server update succeeds, delete the local screenshot file.
                // This is a local operation and is less likely to fail.
                await avatarScreenshotService.DeleteScreenshotAsync(slotIndex, ct);

                outfitsAreDirty = false;
                return true; // Both operations succeeded
            }
            catch (Exception ex)
            {
                ReportHub.LogException(ex, "Failed to deploy outfit deletion immediately.");
                localOutfits.Insert(originalIndex, outfitToRemove);

                return false;
            }
        }
    }
}