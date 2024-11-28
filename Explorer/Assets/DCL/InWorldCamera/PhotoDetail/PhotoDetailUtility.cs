using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Multiplayer.Connections.DecentralandUrls;
using System;

namespace DCL.InWorldCamera.PhotoDetail
{
    public static class PhotoDetailUtility
    {
        public static string GetMarketplaceLink(this IWearable wearable, IDecentralandUrlsSource decentralandUrlsSource)
        {
            var marketplace = $"{decentralandUrlsSource.Url(DecentralandUrl.Market)}/contracts/{{0}}/items/{{1}}";
            ReadOnlySpan<char> idSpan = wearable.GetUrn().ToString().AsSpan();
            int lastColonIndex = idSpan.LastIndexOf(':');

            if (lastColonIndex == -1)
                return "";

            var item = idSpan.Slice(lastColonIndex + 1).ToString();
            idSpan = idSpan.Slice(0, lastColonIndex);
            int secondLastColonIndex = idSpan.LastIndexOf(':');
            var contract = idSpan.Slice(secondLastColonIndex + 1).ToString();

            // If this is not correct, we could retrieve the marketplace link by checking TheGraph, but that's super slow
            if (!contract.StartsWith("0x") || !int.TryParse(item, out int _))
                return "";

            return string.Format(marketplace, contract, item);
        }
    }
}
