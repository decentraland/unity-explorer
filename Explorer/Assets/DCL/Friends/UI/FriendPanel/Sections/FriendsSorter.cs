using System;
using System.Collections.Generic;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public static class FriendsSorter
    {
        internal static void SortFriendList(List<FriendProfile> friends)
        {
            friends.Sort((f1, f2) =>
                string.Compare(f1.Name, f2.Name, StringComparison.OrdinalIgnoreCase)
            );
        }

        internal static void SortFriendRequestList(List<FriendRequest> friends)
        {
            friends.Sort((r1, r2) => r2.Timestamp.CompareTo(r1.Timestamp));
        }
    }
}
