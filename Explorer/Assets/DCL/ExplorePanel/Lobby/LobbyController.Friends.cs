using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Diagnostics;
using DCL.Friends.UI.FriendPanel.Sections;
using DCL.Multiplayer.Connectivity;
using DCL.Passport;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.UI;
using DCL.Utilities.Extensions;
using MVC;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.ExplorePanel.Lobby
{
    /// <summary>Online friends rail: statuses come from the connectivity tracker, locations resolve per shown card.</summary>
    public sealed partial class LobbyController
    {
        private const string LOCATING_TEXT = "Locating…";
        private readonly List<Profile.CompactInfo> onlineFriends = new ();
        private readonly Dictionary<string, string> placeNames = new (StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> resolvingPlaces = new (StringComparer.OrdinalIgnoreCase);

        private void ResetFriends()
        {
            onlineFriends.Clear();
            connectivity?.CopyOnlineFriendsTo(onlineFriends);
            FriendsSorter.SortFriendList(onlineFriends);
            RefreshFriendsList();
        }

        private void ClearFriends()
        {
            placeNames.Clear();
            view.FriendsList.SetListItemCount(0, false);
            view.FriendsSection.SetActive(false);
        }

        private void RefreshFriendsList()
        {
            view.FriendsSection.SetActive(onlineFriends.Count > 0);
            view.FriendsList.SetListItemCount(onlineFriends.Count, false);
            view.FriendsList.RefreshAllShownItem();
            Visit?.Content("friends", onlineFriends.Count, true);
        }

        private void FriendBecameOnline(Profile.CompactInfo friend) =>
            SetFriendOnline(friend, true);

        private void FriendBecameOffline(Profile.CompactInfo friend) =>
            SetFriendOnline(friend, false);

        private void SetFriendOnline(Profile.CompactInfo friend, bool online)
        {
            if (Visit == null) return;
            int index = IndexOfFriend(friend.UserId.Value);
            if (online == (index >= 0)) return;

            if (online)
            {
                onlineFriends.Add(friend);
                FriendsSorter.SortFriendList(onlineFriends);
            }
            else onlineFriends.RemoveAt(index);

            RefreshFriendsList();
        }

        private int IndexOfFriend(string userId)
        {
            for (var i = 0; i < onlineFriends.Count; i++)
                if (string.Equals(onlineFriends[i].UserId.Value, userId, StringComparison.OrdinalIgnoreCase)) return i;

            return -1;
        }

        private LoopListViewItem2 OnGetFriendItem(LoopListView2 list, int index)
        {
            LoopListViewItem2 item = list.NewListViewItem(list.ItemPrefabDataList[0].mItemPrefab.name);
            var card = item.GetComponent<LobbyCardView>();
            Profile.CompactInfo profile = onlineFriends[index];
            string userId = profile.UserId.Value;
            CancellationToken ct = Visit?.Token ?? CancellationToken.None;
            card.Key = userId;
            string detail = placeNames.TryGetValue(userId, out string? placeName) ? placeName : LOCATING_TEXT;

            card.Bind(profile.DisplayName, detail,
                connectivity?.GetFriendStatus(userId) == OnlineStatus.Away ? "Away" : "Online",
                profile.FaceSnapshotUrl.Value, thumbnails, ct,
                () => TravelAsync("friends", "friend", null, index, travelCt => JoinFriendAsync(userId, travelCt)).SuppressToResultAsync(ReportCategory.UI).Forget(),
                () => { Visit?.Action("chat", "friends"); ChatOpener.Instance.OpenPrivateConversationWithUserId(userId); },
                () => { Visit?.Action("profile", "friends"); mvcManager.ShowAndForget(PassportController.IssueCommand(new PassportParams(userId))); });

            if (placeName == null && refreshCts != null)
                ResolvePlaceAsync(userId, card, refreshCts.Token).SuppressToResultAsync(ReportCategory.UI).Forget();

            return item;
        }

        private async UniTask ResolvePlaceAsync(string userId, LobbyCardView card, CancellationToken ct)
        {
            if (!resolvingPlaces.Add(userId)) return;

            try
            {
                string placeName = string.Empty;

                foreach (OnlineUserData location in await onlineUsers.GetAsync(new[] { userId }, ct))
                {
                    if (!string.Equals(location.avatarId, userId, StringComparison.OrdinalIgnoreCase)) continue;
                    placeName = await PlaceNameAsync(location, ct);
                    break;
                }

                if (ct.IsCancellationRequested) return;
                placeNames[userId] = placeName;
                if (card.Key == userId) card.SetDetail(placeName);
            }
            finally { resolvingPlaces.Remove(userId); }
        }

        private async UniTask<string> PlaceNameAsync(OnlineUserData location, CancellationToken ct)
        {
            Vector2Int parcel = location.position.ToParcel();

            PlacesData.PlaceInfo? place = location.IsInWorld
                ? await placesApi.GetWorldAsync(parcel, location.worldName ?? string.Empty, ct)
                : await placesApi.GetPlaceAsync(parcel, ct);

            if (place is { IsEmptyPlace: false }) return place.title;
            return location.IsInWorld ? location.worldName ?? "World" : $"Genesis City ({parcel.x}, {parcel.y})";
        }
    }
}
