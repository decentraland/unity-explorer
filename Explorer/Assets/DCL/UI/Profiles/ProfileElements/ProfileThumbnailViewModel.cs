using System;
using Unity.Burst;
using UnityEngine;

namespace DCL.UI.ProfileElements
{
    public readonly struct ProfileThumbnailViewModel : IEquatable<ProfileThumbnailViewModel>
    {
        private static readonly Color DEFAULT_PROFILE_COLOR = Color.white;

        public enum State : byte
        {
            /// <summary>
            ///     If the view model is not bound the loading won't be started
            /// </summary>
            NotBound,
            Loading,
            LoadedFromCache,
            LoadedRemotely,
            Fallback,
            Error,
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
            new (State.Loading, null, color);

        public static ProfileThumbnailViewModel Default(Color? color = null) =>
            new (State.NotBound, null, color);

        public static ProfileThumbnailViewModel FromFallback(Sprite sprite, Color? color = null) =>
            new (State.Fallback, sprite, color);

        public static ProfileThumbnailViewModel Error(Color? color = null) =>
            new (State.Error, null, color);

        public static ProfileThumbnailViewModel FromLoaded(Sprite sprite, bool fromCache, Color? color = null, bool fitAndCenter = false) =>
            new (fromCache ? State.LoadedFromCache : State.LoadedRemotely, sprite, color, fitAndCenter);

        [BurstDiscard]
        public bool Equals(ProfileThumbnailViewModel other) =>
            ThumbnailState == other.ThumbnailState && Equals(Sprite, other.Sprite) && ProfileColor == other.ProfileColor;

        [BurstDiscard]
        public override bool Equals(object? obj) =>
            obj is ProfileThumbnailViewModel other && Equals(other);

        [BurstDiscard]
        public override int GetHashCode() =>
            HashCode.Combine((int)ThumbnailState, Sprite, ProfileColor);
    }
}
