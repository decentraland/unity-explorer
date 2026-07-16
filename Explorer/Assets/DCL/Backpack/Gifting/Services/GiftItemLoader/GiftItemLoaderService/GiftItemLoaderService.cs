using System;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Models;
using DCL.Backpack.Gifting.Services.GiftItemLoader;
using DCL.Diagnostics;
using DCL.WebRequests;
using UnityEngine;

namespace DCL.Backpack.Gifting.Services.GiftItemLoaderService
{
    public class GiftItemLoaderService : IGiftItemLoaderService
    {
        private readonly IWebRequestController webRequestController;

        public GiftItemLoaderService(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        public async UniTask<GiftItemModel?> LoadItemMetadataAsync(string tokenUri, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(tokenUri)) return null;

            try
            {
                URLAddress url = URLAddress.FromString(tokenUri);

                GiftItemResponseDTO dto = await webRequestController
                    .GetAsync(url, ct, ReportCategory.GIFTING)
                    .CreateFromJson<GiftItemResponseDTO>(WRJsonParser.Unity);

                if (dto == null) return null;

                // NOTE: what are the defaults?
                string rarity = "common";
                string category = "wearable";

                if (dto.attributes != null)
                {
                    foreach (var attr in dto.attributes)
                    {
                        string trait = attr.trait_type?.ToLower() ?? "";
                        string val = attr.value?.ToLower() ?? "";

                        switch (trait)
                        {
                            case "rarity":
                                rarity = val;
                                break;
                            case "category":
                                category = val;
                                break;
                        }
                    }
                }

                string name = dto.name ?? string.Empty;
                string description = dto.description ?? string.Empty;
                string imageUrl =
                    !string.IsNullOrEmpty(dto.thumbnail) ? dto.thumbnail :
                    !string.IsNullOrEmpty(dto.image) ? dto.image :
                    string.Empty;

                return new GiftItemModel
                {
                    Name = name, Description = description, ImageUrl = imageUrl,
                    Rarity = rarity,
                    Category = category
                };
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.GIFTING));
                return null;
            }
        }
    }
}