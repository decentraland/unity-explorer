using System;
using System.Collections.Generic;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public static class FriendsSorter
    {
        private static readonly Comparison<FriendProfile> CACHED_PROFILE_COMPARISON = FriendProfileComparison;
        private static readonly Comparison<FriendRequest> CACHED_REQUEST_COMPARISON = FriendRequestComparison;

        internal static void SortFriendList<T>(List<T> friends) where T : FriendProfile =>
            friends.Sort(CACHED_PROFILE_COMPARISON);

        internal static void SortFriendRequestList(List<FriendRequest> friends) =>
            friends.Sort(CACHED_REQUEST_COMPARISON);

        private static int FriendProfileComparison(FriendProfile f1, FriendProfile f2) =>
            string.Compare(f1.Name, f2.Name, StringComparison.OrdinalIgnoreCase);

        private static int FriendRequestComparison(FriendRequest r1, FriendRequest r2) =>
            r2.Timestamp.CompareTo(r1.Timestamp);
    }
}
