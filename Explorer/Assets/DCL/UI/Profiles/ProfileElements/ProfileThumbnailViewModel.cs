using DCL.Utilities;
using System;
using UnityEngine;

namespace DCL.UI.ProfileElements
{
    public readonly struct ProfileThumbnailViewModel : IEquatable<ProfileThumbnailViewModel>
    {
        public static readonly Color DEFAULT_PROFILE_COLOR = Color.white;

        public enum State : byte
        {
            /// <summary>
            ///     If the view model is not bound the loading won't be started
            /// </summary>
            NOT_BOUND,
            LOADING,
            LOADED_FROM_CACHE,
            LOADED_REMOTELY,
            FALLBACK,
            ERROR,
        }

        public readonly State ThumbnailState;
        public readonly Sprite? Sprite;
        public readonly bool FitAndCenterImage;
        public readonly Color ProfileColor;

        internal ProfileThumbnailViewModel(State thumbnailState, Sprite? sprite, Color? profileColor = null, bool fitAndCenterImage = false)
        {
            ThumbnailState = thumbnailState;
            Sprite = sprite;
            ProfileColor = profileColor ?? DEFAULT_PROFILE_COLOR;
            FitAndCenterImage = fitAndCenterImage;
        }

        public static ProfileThumbnailViewModel ReadyToLoad(Color? color = null) =>
            new (State.LOADING, null, color);

        public static ProfileThumbnailViewModel Default(Color? color = null) =>
            new (State.NOT_BOUND, null, color);

        public static ProfileThumbnailViewModel FromFallback(Sprite sprite, Color? color = null) =>
            new (State.FALLBACK, sprite, color);

        public static ProfileThumbnailViewModel Error(Color? color = null) =>
            new (State.ERROR, null, color);

        public static ProfileThumbnailViewModel FromLoaded(Sprite sprite, bool fromCache, Color? color = null, bool fitAndCenter = false) =>
            new (fromCache ? State.LOADED_FROM_CACHE : State.LOADED_REMOTELY, sprite, color, fitAndCenter);

        public bool Equals(ProfileThumbnailViewModel other) =>
            ThumbnailState == other.ThumbnailState && Equals(Sprite, other.Sprite) && ProfileColor == other.ProfileColor;

        public override bool Equals(object? obj) =>
            obj is ProfileThumbnailViewModel other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine((int)ThumbnailState, Sprite, ProfileColor);
    }
}
