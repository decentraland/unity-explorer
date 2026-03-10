using DCL.Profiles;
using System;
using System.Collections.Generic;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public static class FriendsSorter
    {
        private static readonly Comparison<Profile.CompactInfo> CACHED_PROFILE_COMPARISON = FriendProfileComparison;
        private static readonly Comparison<FriendRequest> CACHED_REQUEST_COMPARISON = FriendRequestComparison;

        public static void SortFriendList(List<BlockedProfile> blockedProfiles) =>
            blockedProfiles.Sort(static (r1, r2) => string.Compare(r1.Profile.Name, r2.Profile.Name, StringComparison.CurrentCulture));

        internal static void SortFriendList(List<Profile.CompactInfo> friends) =>
            friends.Sort(CACHED_PROFILE_COMPARISON);

        internal static void SortFriendRequestList(List<FriendRequest> friends) =>
            friends.Sort(CACHED_REQUEST_COMPARISON);

        private static int FriendProfileComparison(Profile.CompactInfo f1, Profile.CompactInfo f2) =>
            string.Compare(f1.Name, f2.Name, StringComparison.OrdinalIgnoreCase);

        private static int FriendRequestComparison(FriendRequest r1, FriendRequest r2) =>
            r2.Timestamp.CompareTo(r1.Timestamp);
    }
}
