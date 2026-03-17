using DCL.Utilities;
using UnityEngine;
using static DCL.UI.ProfileElements.ProfileThumbnailViewModel;

namespace DCL.UI.ProfileElements
{
    public static class ProfileThumbnailViewModelExtensions
    {
        public static void SetLoading(this IReactiveProperty<ProfileThumbnailViewModel> property, Color color) =>
            property.UpdateValue(ReadyToLoad());

        public static void SetLoaded(this IReactiveProperty<ProfileThumbnailViewModel> property, Sprite sprite, bool fromCache) =>
            property.UpdateValue(FromLoaded(sprite, fromCache, property.Value.FitAndCenterImage));

        public static void TryBind(this IReactiveProperty<ProfileThumbnailViewModel> property)
        {
            if (property.Value.ThumbnailState == State.NOT_BOUND)
                property.UpdateValue(property.Value.TryBind());
        }
    }
}
