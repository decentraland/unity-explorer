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
        private readonly URLBuilder urlBuilder = new();

        public GiftItemLoaderService(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        public async UniTask<GiftItemModel?> LoadItemMetadataAsync(string tokenUri, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(tokenUri)) return null;

            try
            {
                urlBuilder.Clear();
                URLAddress url = URLAddress.FromString(tokenUri);

                GiftItemResponseDTO dto = await webRequestController
                    .GetAsync(url, ct, ReportCategory.GIFTING)
                    .CreateFromJson<GiftItemResponseDTO>(WRJsonParser.Unity);

                if (dto == null) return null;
                
                string rarity = "common";
                string category = "wearable";

                if (dto.attributes != null)
                {
                    foreach (var attr in dto.attributes)
                    {
                        string trait = attr.trait_type.ToLower();
                        string val = attr.value.ToLower();

                        if (trait == "rarity") rarity = val;
                        else if (trait == "category") category = val;
                    }
                }

                return new GiftItemModel
                {
                    Name = dto.name,
                    Description = dto.description,
                    ImageUrl = !string.IsNullOrEmpty(dto.thumbnail) ? dto.thumbnail : dto.image,
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