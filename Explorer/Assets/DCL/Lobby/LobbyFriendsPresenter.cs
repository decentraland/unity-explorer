using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Friends.UI.FriendPanel.Sections;
using DCL.Multiplayer.Connectivity;
using DCL.Passport;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.Lobby
{
    /// <summary>
    ///     Online friends rail of the lobby: the list mirrors the connectivity tracker, each shown card resolves where its friend is
    ///     (place title, or "Lobby" when the friend has no position in the world) and Join hands the friend's live position to the owner.
    /// </summary>
    public class LobbyFriendsPresenter : IDisposable
    {
        private const string LOBBY_LABEL = "Lobby";
        private const string ONLINE_COUNT_SUFFIX = " Online";

        /// <summary>
        ///     Raised with the friend's live position when Join is pressed and the friend is still in the world.
        /// </summary>
        public Action<OnlineUserData>? JoinRequested;

        private readonly LobbyFriendsSectionView view;
        private readonly FriendsConnectivityStatusTracker tracker;
        private readonly IOnlineUsersProvider onlineUsers;
        private readonly IPlacesAPIService places;
        private readonly IPassportBridge passport;
        private readonly List<Profile.CompactInfo> onlineFriends = new ();
        private readonly Dictionary<string, FriendLocation> locations = new (StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> resolving = new (StringComparer.OrdinalIgnoreCase);
        private readonly List<string> pendingIds = new ();
        private readonly List<LobbyFriendCardView> wiredCards = new ();
        private readonly string[] joinIdBuffer = new string[1];

        private CancellationToken showCt;
        private CancellationTokenSource? jumpCts;
        private bool flushScheduled;

        public LobbyFriendsPresenter(LobbyFriendsSectionView view,
            FriendsConnectivityStatusTracker tracker,
            IOnlineUsersProvider onlineUsers,
            IPlacesAPIService places,
            IPassportBridge passport)
        {
            this.view = view;
            this.tracker = tracker;
            this.onlineUsers = onlineUsers;
            this.places = places;
            this.passport = passport;

            view.Rail.Init(OnGetItem);
            view.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            Hide();
            JoinRequested = null;

            foreach (LobbyFriendCardView card in wiredCards)
            {
                card.Clicked = null;
                card.JoinClicked = null;
            }
        }

        /// <summary>
        ///     Lists the friends currently online and follows the tracker until <see cref="Hide" />. Locations are resolved again on
        ///     every show: the previous ones may be minutes old.
        /// </summary>
        public void Show(CancellationToken ct)
        {
            showCt = ct;
            locations.Clear();
            resolving.Clear();
            pendingIds.Clear();
            flushScheduled = false;

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

        /// <summary>
        ///     The tracker is the single source of truth: the list is re-read from it in full on every change, sorted like the friends panel.
        /// </summary>
        private void Rebuild(bool rewind)
        {
            onlineFriends.Clear();
            tracker.CopyOnlineFriendsTo(onlineFriends);
            FriendsSorter.SortFriendList(onlineFriends);

            int count = onlineFriends.Count;
            view.gameObject.SetActive(count > 0);
            view.OnlineCountText.text = string.Concat(count.ToString(), ONLINE_COUNT_SUFFIX);
            view.Rail.SetCount(count, rewind);
            view.Rail.RefreshShown();
        }

        private LoopListViewItem2 OnGetItem(LoopListView2 _, int index)
        {
            LoopListViewItem2 item = view.Rail.NewItem(out LobbyFriendCardView card, out bool created);

            if (created)
            {
                card.Clicked = OpenPassport;
                card.JoinClicked = Join;
                wiredCards.Add(card);
            }

            Profile.CompactInfo profile = onlineFriends[index];
            string userId = profile.UserId.Value;
            card.Bind(profile, tracker.GetFriendStatus(userId), showCt);

            if (locations.TryGetValue(userId, out FriendLocation location))
                card.SetLocation(location.Label, location.CanJoin);
            else
            {
                card.SetLocating();
                QueueLocation(userId);
            }

            return item;
        }

        private void QueueLocation(string userId)
        {
            if (!resolving.Add(userId)) return;

            pendingIds.Add(userId);

            if (flushScheduled) return;

            flushScheduled = true;
            FlushLocationsAsync(showCt).Forget();
        }

        /// <summary>
        ///     Waits a frame so every card bound in the same pass (a whole page) shares one positions request.
        /// </summary>
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

            foreach (string id in ids)
            {
                OnlineUserData? data = Find(result.Value, id);
                string label = data.HasValue ? await PlaceNameAsync(data.Value, ct) : LOBBY_LABEL;

                if (ct.IsCancellationRequested) return;

                locations[id] = new FriendLocation(label, canJoin: data.HasValue);
                resolving.Remove(id);
                ApplyLocation(id, label, data.HasValue);
            }
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
            for (var i = 0; i < view.Rail.ShownCount; i++)
            {
                LobbyFriendCardView card = view.Rail.ShownCardAt(i);

                if (string.Equals(card.UserId, userId, StringComparison.OrdinalIgnoreCase))
                    card.SetLocation(label, canJoin);
            }
        }

        private void Join(LobbyFriendCardView card)
        {
            jumpCts = jumpCts.SafeRestart();
            JoinAsync(card.UserId, jumpCts.Token).Forget();
        }

        /// <summary>
        ///     The shown location may be stale, so the live position is fetched again before jumping.
        /// </summary>
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

        private void OpenPassport(LobbyFriendCardView card) =>
            passport.ShowAsync(card.UserId).SuppressToResultAsync(ReportCategory.UI).Forget();

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
    }
}
