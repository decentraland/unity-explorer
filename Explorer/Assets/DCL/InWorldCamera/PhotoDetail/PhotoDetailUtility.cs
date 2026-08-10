using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Multiplayer.Connections.DecentralandUrls;
using System;

namespace DCL.InWorldCamera.PhotoDetail
{
    public static class PhotoDetailUtility
    {
        /// <summary>
        ///     Builds the Shop link for a wearable worn in a photo.
        ///     <para>
        ///         The Shop and not the marketplace: the subject here is always a wearable, which is what the Shop
        ///         sells and prices in USD. LAND still belongs to the marketplace — the Shop carries no parcels.
        ///     </para>
        ///     <para>
        ///         Note the route differs, so this is not a host swap: the marketplace addresses an item as
        ///         <c>/contracts/{contract}/items/{id}</c> and the Shop as <c>/item/{contract}/{id}</c>. Keeping the
        ///         old shape against the new host is a 404, not a redirect.
        ///     </para>
        /// </summary>
        public static string GetShopLink(this IWearable wearable, IDecentralandUrlsSource decentralandUrlsSource)
        {
            var shop = $"{decentralandUrlsSource.Url(DecentralandUrl.ShopLink)}/item/{{0}}/{{1}}";
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

            return string.Format(shop, contract, item);
        }
    }
}
