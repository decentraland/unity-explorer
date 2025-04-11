using System;
using System.Collections.Generic;

namespace DCL.Chat
{
    public interface IChatUsersStateCache
    {
        void AddConnectedUser(string userAddress);
        void RemoveConnectedUser(string userAddress);

        void AddConnectedBlockedUser(string userAddress);
        void RemovedConnectedBlockedUser(string userAddress);

        bool IsBlockedUserConnected(string userAddress);
        bool IsUserConnected(string userAddress);
    }

    public class ChatUsersStateCache : IChatUsersStateCache
    {
        /// <summary>
        /// This stores all currently connected Users (that are not blocked)
        /// </summary>
        private readonly HashSet<string> connectedUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// This stores all currently connected Blocked users, its updated each time a user connects or disconnects from the Livekit Chat Room
        /// </summary>
        private readonly HashSet<string> connectedBlockedUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);


        public void AddConnectedUser(string userAddress) => connectedUsers.Add(userAddress);
        public void AddConnectedBlockedUser(string userAddress) => connectedBlockedUsers.Add(userAddress);
        public void RemoveConnectedUser(string userAddress) => connectedUsers.Remove(userAddress);
        public void RemovedConnectedBlockedUser(string userAddress) => connectedBlockedUsers.Remove(userAddress);

        public void ClearAll()
        {
            connectedUsers.Clear();
            connectedBlockedUsers.Clear();
        }

        public bool IsUserConnected(string userAddress) =>
            connectedUsers.Contains(userAddress);
        public bool IsBlockedUserConnected(string userAddress) =>
            connectedBlockedUsers.Contains(userAddress);
    }
}
