using DCL.Profiles;
using DCL.Web3;

namespace DCL.Friends.UI.Requests
{
    public struct FriendRequestParams
    {
        /// <summary>
        /// null means send new request
        /// </summary>
        public FriendRequest? Request;
        /// <summary>
        /// Needed in case request is null
        /// </summary>
        public Web3Address? DestinationUser;
        /// <summary>
        /// Needed in case the request was accepted from another flow, and we need to display the modal
        /// </summary>
        public Profile.CompactInfo? OneShotFriendAccepted;
    }
}
