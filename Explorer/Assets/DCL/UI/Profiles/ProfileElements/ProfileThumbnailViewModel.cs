using System;
using UnityEngine;

namespace DCL.UI.ProfileElements
{
    public readonly struct ProfileThumbnailViewModel : IEquatable<ProfileThumbnailViewModel>
    {
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
