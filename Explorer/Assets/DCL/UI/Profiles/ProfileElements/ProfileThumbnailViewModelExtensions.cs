using DCL.Utilities;
using UnityEngine;
using static DCL.UI.ProfileElements.ProfileThumbnailViewModel;

namespace DCL.UI.ProfileElements
{
    public static class ProfileThumbnailViewModelExtensions
    {
        public static void SetLoading(this IReactiveProperty<ProfileThumbnailViewModel> property, Color color) =>
            property.UpdateValue(new ProfileThumbnailViewModel(State.LOADING, null, color));

        public static void SetLoaded(this IReactiveProperty<ProfileThumbnailViewModel> property, Sprite sprite, bool fromCache) =>
            property.UpdateValue(FromLoaded(sprite, fromCache, property.Value.ProfileColor, property.Value.FitAndCenterImage));

        public static void SetColor(this IReactiveProperty<ProfileThumbnailViewModel> property, Color color) =>
            property.UpdateValue(new ProfileThumbnailViewModel(property.Value.ThumbnailState, property.Value.Sprite, color, property.Value.FitAndCenterImage));

        public static void TryBind(this IReactiveProperty<ProfileThumbnailViewModel> property)
        {
            if (property.Value.ThumbnailState == State.NOT_BOUND)

                property.UpdateValue(new ProfileThumbnailViewModel(State.LOADING, property.Value.Sprite, property.Value.ProfileColor, property.Value.FitAndCenterImage));
        }
    }
}
