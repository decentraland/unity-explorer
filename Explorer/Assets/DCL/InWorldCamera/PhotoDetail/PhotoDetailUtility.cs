using DCL.AvatarRendering.Wearables.Components;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Shop;

namespace DCL.InWorldCamera.PhotoDetail
{
    public static class PhotoDetailUtility
    {
        /// <summary>
        ///     The web shop link of a wearable; empty when it is not a collection item.
        /// </summary>
        public static string GetMarketplaceLink(this IWearable wearable, IDecentralandUrlsSource decentralandUrlsSource) =>
            ShopItemLinks.BuildItemUrlFromUrn(decentralandUrlsSource, wearable.GetUrn().ToString());
    }
}
