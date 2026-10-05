using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Friends.UI.FriendPanel.Sections;
using DCL.Multiplayer.Connectivity;
using DCL.Passport;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.UI.ProfileElements;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UIElements;
using Utility;

namespace DCL.Lobby
{
    /// <summary>
    ///     Fills the document's online friends row from the connectivity tracker and resolves where each friend is.
    /// </summary>
    public class LobbyDocumentFriendsPresenter : IDisposable
    {
        private const string LOBBY_LABEL = "Lobby";
        private const string LOCATING_LABEL = "Locating…";

        /// <summary>
        ///     Raised with the friend's live position when Join is pressed and the friend is still in the world.
        /// </summary>
        public Action<OnlineUserData>? JoinRequested;

        private readonly LobbyFriendsRail rail;
        private readonly FriendsConnectivityStatusTracker tracker;
        private readonly IOnlineUsersProvider onlineUsers;
        private readonly IPlacesAPIService places;
        private readonly IPassportBridge passport;
        private readonly List<Profile.CompactInfo> onlineFriends = new ();

        // One per card of the rail, in the same order
        private readonly List<CardBinding> bindings = new ();
        private readonly Dictionary<string, FriendLocation> locations = new (StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> resolving = new (StringComparer.OrdinalIgnoreCase);
        private readonly List<string> pendingIds = new ();
        private readonly string[] joinIdBuffer = new string[1];

        private CancellationToken showCt;
        private CancellationTokenSource? jumpCts;
        private bool flushScheduled;

        public LobbyDocumentFriendsPresenter(LobbyFriendsRail rail,
            FriendsConnectivityStatusTracker tracker,
            IOnlineUsersProvider onlineUsers,
            IPlacesAPIService places,
            IPassportBridge passport)
        {
            this.rail = rail;
            this.tracker = tracker;
            this.onlineUsers = onlineUsers;
            this.places = places;
            this.passport = passport;

            rail.CardClicked = OpenPassport;
            rail.CardJoinClicked = Join;
        }

        public void Dispose()
        {
            Hide();
            JoinRequested = null;
            rail.CardClicked = null;
            rail.CardJoinClicked = null;

            foreach (CardBinding binding in bindings)
                binding.Dispose();
        }

        /// <summary>
        ///     Takes over the friends section of the current hierarchy and resolves the stale locations again.
        /// </summary>
        public void Show(VisualElement section, CancellationToken ct)
        {
            showCt = ct;
            locations.Clear();
            resolving.Clear();
            pendingIds.Clear();
            flushScheduled = false;
            rail.Show(section);

            tracker.OnFriendBecameOnline += OnFriendStatusChanged;
            tracker.OnFriendBecameAway += OnFriendStatusChanged;
            tracker.OnFriendBecameOffline += OnFriendStatusChanged;
            tracker.OnFriendRemoved += OnFriendRemoved;
            tracker.OnReset += OnTrackerReset;

            Rebuild(rewind: true);
        }

        public void Hide()
        {
            tracker.OnFriendBecameOnline -= OnFriendStatusChanged;
            tracker.OnFriendBecameAway -= OnFriendStatusChanged;
            tracker.OnFriendBecameOffline -= OnFriendStatusChanged;
            tracker.OnFriendRemoved -= OnFriendRemoved;
            tracker.OnReset -= OnTrackerReset;
            jumpCts.SafeCancelAndDispose();
            rail.Hide();
        }

        private void OnFriendStatusChanged(Profile.CompactInfo _) =>
            Rebuild(rewind: false);

        private void OnFriendRemoved(string _) =>
            Rebuild(rewind: false);

        private void OnTrackerReset()
        {
            locations.Clear();
            Rebuild(rewind: false);
        }

        private void Rebuild(bool rewind)
        {
            onlineFriends.Clear();
            tracker.CopyOnlineFriendsTo(onlineFriends);
            FriendsSorter.SortFriendList(onlineFriends);

            rail.SetCount(onlineFriends.Count, rewind);

            for (var i = 0; i < onlineFriends.Count; i++)
                Bind(i);
        }

        private void Bind(int index)
        {
            LobbyFriendCardElement card = rail.Cards[index];

            while (bindings.Count <= index)
                bindings.Add(new CardBinding(rail.Cards[bindings.Count]));

            CardBinding binding = bindings[index];
            Profile.CompactInfo profile = onlineFriends[index];
            string userId = profile.UserId.Value;

            card.UserName = profile.Name;
            card.UserNameColor = profile.UserNameColor;
            card.WalletTag = profile.HasClaimedName ? string.Empty : profile.WalletId ?? string.Empty;
            card.IsVerified = profile.HasClaimedName;
            card.OnlineStatus = tracker.GetFriendStatus(userId);

            // Rebinding the same friend keeps the picture unless its fetch was cancelled by a hide before finishing
            if (!string.Equals(binding.UserId, userId, StringComparison.OrdinalIgnoreCase) || binding.IsPictureLoading)
                binding.LoadPicture(profile, showCt);

            if (locations.TryGetValue(userId, out FriendLocation location))
                ShowLocation(card, location.Label, location.CanJoin);
            else
            {
                ShowLocation(card, LOCATING_LABEL, canJoin: false);
                QueueLocation(userId);
            }
        }

        private static void ShowLocation(LobbyFriendCardElement card, string label, bool canJoin)
        {
            card.Location = label;
            card.CanJoin = canJoin;
        }

        private void QueueLocation(string userId)
        {
            if (!resolving.Add(userId)) return;

            pendingIds.Add(userId);

            if (flushScheduled) return;

            flushScheduled = true;
            FlushLocationsAsync(showCt).Forget();
        }

        // Waits a frame so every card bound in the same pass shares one positions request
        private async UniTaskVoid FlushLocationsAsync(CancellationToken ct)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow();
            flushScheduled = false;

            if (ct.IsCancellationRequested) return;

            using PooledObject<List<string>> scope = ListPool<string>.Get(out List<string> ids);
            ids.AddRange(pendingIds);
            pendingIds.Clear();

            Result<IReadOnlyCollection<OnlineUserData>> result = await onlineUsers.GetAsync(ids, ct).SuppressToResultAsync(ReportCategory.FRIENDS);

            if (ct.IsCancellationRequested) return;

            // Leave the cards in their locating state: the next bind retries
            if (!result.Success)
            {
                foreach (string id in ids)
                    resolving.Remove(id);

                return;
            }

            using PooledObject<List<UniTask>> tasksScope = ListPool<UniTask>.Get(out List<UniTask> tasks);

            foreach (string id in ids)
                tasks.Add(ResolveLocationAsync(id, Find(result.Value, id), ct));

            await UniTask.WhenAll(tasks);
        }

        private async UniTask ResolveLocationAsync(string userId, OnlineUserData? data, CancellationToken ct)
        {
            string label = data.HasValue ? await PlaceNameAsync(data.Value, ct) : LOBBY_LABEL;

            if (ct.IsCancellationRequested) return;

            locations[userId] = new FriendLocation(label, canJoin: data.HasValue);
            resolving.Remove(userId);
            ApplyLocation(userId, label, data.HasValue);
        }

        private async UniTask<string> PlaceNameAsync(OnlineUserData data, CancellationToken ct)
        {
            Vector2Int parcel = data.position.ToParcel();

            Result<PlacesData.PlaceInfo?> place = await (data.worldName is { Length: > 0 } worldName
                    ? places.GetWorldAsync(parcel, worldName, ct)
                    : places.GetPlaceAsync(parcel, ct))
               .SuppressToResultAsync(ReportCategory.PLACES);

            if (place.Success && place.Value is { IsEmptyPlace: false } info && !string.IsNullOrEmpty(info.title))
                return info.title;

            return data.worldName is { Length: > 0 } ? data.worldName : $"{parcel.x},{parcel.y}";
        }

        private void ApplyLocation(string userId, string label, bool canJoin)
        {
            for (var i = 0; i < rail.Count; i++)
                if (string.Equals(bindings[i].UserId, userId, StringComparison.OrdinalIgnoreCase))
                    ShowLocation(rail.Cards[i], label, canJoin);
        }

        private void Join(int index)
        {
            jumpCts = jumpCts.SafeRestart();
            JoinAsync(bindings[index].UserId, jumpCts.Token).Forget();
        }

        // The shown location may be stale, so the live position is fetched again before jumping
        private async UniTaskVoid JoinAsync(string userId, CancellationToken ct)
        {
            joinIdBuffer[0] = userId;
            Result<IReadOnlyCollection<OnlineUserData>> result = await onlineUsers.GetAsync(joinIdBuffer, ct).SuppressToResultAsync(ReportCategory.FRIENDS);

            if (ct.IsCancellationRequested || !result.Success) return;

            OnlineUserData? data = Find(result.Value, userId);

            if (data.HasValue)
            {
                JoinRequested?.Invoke(data.Value);
                return;
            }

            locations[userId] = new FriendLocation(LOBBY_LABEL, canJoin: false);
            ApplyLocation(userId, LOBBY_LABEL, canJoin: false);
        }

        private void OpenPassport(int index) =>
            passport.ShowAsync(bindings[index].UserId).SuppressToResultAsync(ReportCategory.UI).Forget();

        private static OnlineUserData? Find(IReadOnlyCollection<OnlineUserData> users, string userId)
        {
            foreach (OnlineUserData user in users)
                if (string.Equals(user.avatarId, userId, StringComparison.OrdinalIgnoreCase))
                    return user;

            return null;
        }

        private readonly struct FriendLocation
        {
            public readonly string Label;
            public readonly bool CanJoin;

            public FriendLocation(string label, bool canJoin)
            {
                Label = label;
                CanJoin = canJoin;
            }
        }

        private class CardBinding : IDisposable
        {
            private readonly LobbyFriendCardElement card;
            private readonly ReactiveProperty<ProfileThumbnailViewModel> thumbnail = new (ProfileThumbnailViewModel.Default());

            private CancellationTokenSource? thumbnailCts;

            public string UserId { get; private set; } = string.Empty;

            // Also true when the fetch was cancelled midway
            public bool IsPictureLoading => thumbnail.Value.ThumbnailState is ProfileThumbnailViewModel.State.Loading or ProfileThumbnailViewModel.State.NotBound;

            public CardBinding(LobbyFriendCardElement card)
            {
                this.card = card;
                thumbnail.Subscribe(OnThumbnailUpdated);
            }

            public void Dispose()
            {
                thumbnailCts.SafeCancelAndDispose();
                thumbnail.Unsubscribe(OnThumbnailUpdated);
            }

            public void LoadPicture(in Profile.CompactInfo profile, CancellationToken ct)
            {
                UserId = profile.UserId.Value;
                thumbnailCts = thumbnailCts.SafeRestartLinked(ct);
                thumbnail.SetLoading(profile.UserNameColor);
                GetProfileThumbnailCommand.Instance.ExecuteAsync(thumbnail, null, profile, thumbnailCts.Token).SuppressToResultAsync(ReportCategory.UI).Forget();
            }

            // The profile color fills the circle without a picture; a fetch keeps any previous picture up
            private void OnThumbnailUpdated(ProfileThumbnailViewModel model)
            {
                card.PictureColor = model.ProfileColor;
                card.Picture = model.Sprite;
                card.IsLoading = model.ThumbnailState == ProfileThumbnailViewModel.State.Loading && model.Sprite == null;
            }
        }
    }
}
