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
    }
}
