using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.UI.ProfileElements;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using System;
using System.Threading;
using Utility;

namespace DCL.Lobby
{
    /// <summary>
    ///     Owns the thumbnail fetch of one profile picture and reports each state of it to the given callback.
    /// </summary>
    public class LobbyProfilePictureBinding : IDisposable
    {
        private readonly ReactiveProperty<ProfileThumbnailViewModel> thumbnail = new (ProfileThumbnailViewModel.Default());
        private readonly Action<ProfileThumbnailViewModel> onUpdated;

        private CancellationTokenSource? cts;

        public string UserId { get; private set; } = string.Empty;

        // Also true when the fetch was cancelled midway
        public bool IsLoading => thumbnail.Value.ThumbnailState is ProfileThumbnailViewModel.State.Loading or ProfileThumbnailViewModel.State.NotBound;

        public LobbyProfilePictureBinding(Action<ProfileThumbnailViewModel> onUpdated)
        {
            this.onUpdated = onUpdated;
            thumbnail.Subscribe(onUpdated);
        }

        public void Dispose()
        {
            cts.SafeCancelAndDispose();
            thumbnail.Unsubscribe(onUpdated);
        }

        public void Load(in Profile.CompactInfo profile, CancellationToken ct)
        {
            // The same user keeps the picture unless its fetch was cancelled before finishing
            if (string.Equals(UserId, profile.UserId.Value, StringComparison.OrdinalIgnoreCase) && !IsLoading)
                return;

            UserId = profile.UserId.Value;
            cts = cts.SafeRestartLinked(ct);
            thumbnail.SetLoading(profile.UserNameColor);
            GetProfileThumbnailCommand.Instance.ExecuteAsync(thumbnail, null, profile, cts.Token).SuppressToResultAsync(ReportCategory.UI).Forget();
        }

        public void Clear()
        {
            cts.SafeCancelAndDispose();
            cts = null;
            UserId = string.Empty;
            thumbnail.UpdateValue(ProfileThumbnailViewModel.Default());
        }
    }
}
