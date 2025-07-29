using DCL.Utilities;
using System;
using UnityEngine;

namespace DCL.UI.ProfileElements
{
    public readonly struct ProfileThumbnailViewModel : IEquatable<ProfileThumbnailViewModel>
    {
        /// <summary>
        ///     The color is not provided at the moment of binding, and updated with the thumbnail at once
        /// </summary>
        public readonly struct WithColor : IEquatable<WithColor>
        {
            public static readonly Color DEFAULT_PROFILE_COLOR = Color.gray;

            public readonly ProfileThumbnailViewModel Thumbnail;
            public readonly Color ProfileColor;

            public WithColor(ProfileThumbnailViewModel thumbnail, Color profileColor)
            {
                ProfileColor = profileColor;
                Thumbnail = thumbnail;
            }

            public WithColor SetColor(Color color) =>
                new (Thumbnail, color);

            public WithColor SetProfile(ProfileThumbnailViewModel thumbnail) =>
                new (thumbnail, ProfileColor);

            public bool Equals(WithColor other) =>
                Thumbnail.Equals(other.Thumbnail) && ProfileColor.Equals(other.ProfileColor);

            public override bool Equals(object? obj) =>
                obj is WithColor other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine(Thumbnail, ProfileColor);

            public static WithColor Default() =>
                new (ProfileThumbnailViewModel.Default(), Color.white);

            public static IReactiveProperty<WithColor> DefaultReactive() =>
                new ReactiveProperty<WithColor>(Default());
        }

        public enum State : byte
        {
            LOADING,
            LOADED_FROM_CACHE,
            LOADED_REMOTELY,
            FALLBACK,
            ERROR,
        }

        public readonly State ThumbnailState;
        public readonly Sprite? Sprite;

        private ProfileThumbnailViewModel(State thumbnailState, Sprite? sprite)
        {
            ThumbnailState = thumbnailState;
            Sprite = sprite;
        }

        public static ProfileThumbnailViewModel Default() =>
            new (State.LOADING, null);

        public static ProfileThumbnailViewModel FromFallback(Sprite sprite) =>
            new (State.FALLBACK, sprite);

        public static ProfileThumbnailViewModel Error() =>
            new (State.ERROR, null);

        public static ProfileThumbnailViewModel FromLoaded(Sprite sprite, bool fromCache) =>
            new (fromCache ? State.LOADED_FROM_CACHE : State.LOADED_REMOTELY, sprite);

        public bool Equals(ProfileThumbnailViewModel other) =>
            ThumbnailState == other.ThumbnailState && Equals(Sprite, other.Sprite);

        public override bool Equals(object? obj) =>
            obj is ProfileThumbnailViewModel other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine((int)ThumbnailState, Sprite);
    }
}
