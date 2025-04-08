using System;
using System.Collections.Generic;

namespace DCL.Chat
{
    public interface IChatUsersStateCache
    {
        HashSet<string> ConnectedNonFriend { get; }
        void AddConnectedFriend(string userAddress);
        void AddConnectedNonFriend(string userAddress);
        void AddUserUnavailableToChat(string userAddress);
        void RemoveConnectedFriend(string userAddress);
        void RemoveConnectedNonFriend(string userAddress);
        void RemoveUserUnavailableToChat(string userAddress);
        void AddUsersUnavailableToChat(IEnumerable<string> addresses);
        void RemoveUsersUnavailableToChat(IEnumerable<string> addresses);

        void AddConnectedBlockedUser(string userAddress);
        void RemovedConnectedBlockedUser(string userAddress);

        bool IsFriendConnected(string userAddress);
        bool IsNonFriendConnected(string userAddress);
        bool IsBlockedUserConnected(string userAddress);

        bool IsUserConnected(string userAddress);
        bool IsUserUnavailableToChat(string userAddress);
    }

    public class ChatUsersStateCache : IChatUsersStateCache
    {
        /// <summary>
        /// This stores all currently connected Friends, its updated each time a friend connects or disconnects from the Livekit Chat Room
        /// </summary>
        private readonly HashSet<string> connectedFriends = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// This stores all currently connected Non-Blocked & Non-Friends users that have set their privacy settings to only receive messages from friends
        /// </summary>
        private readonly HashSet<string> usersUnavailableToChat = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// This stores all currently connected Non-Blocked & Non-Friends users, its updated each time a user connects or disconnects from the Livekit Chat Room
        /// </summary>
        public HashSet<string> ConnectedNonFriend { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> connectedBlockedUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);


        public void AddConnectedFriend(string userAddress) => connectedFriends.Add(userAddress);
        public void AddConnectedNonFriend(string userAddress) => ConnectedNonFriend.Add(userAddress);
        public void AddConnectedBlockedUser(string userAddress) => connectedBlockedUsers.Add(userAddress);
        public void AddUserUnavailableToChat(string userAddress) => usersUnavailableToChat.Add(userAddress);

        public void RemoveConnectedFriend(string userAddress) => connectedFriends.Remove(userAddress);
        public void RemoveConnectedNonFriend(string userAddress) => ConnectedNonFriend.Remove(userAddress);
        public void RemovedConnectedBlockedUser(string userAddress) => connectedBlockedUsers.Remove(userAddress);
        public void RemoveUserUnavailableToChat(string userAddress) => usersUnavailableToChat.Remove(userAddress);

        public void AddConnectedFriends(IEnumerable<string> addresses)
        {
            foreach (string address in addresses)
                connectedFriends.Add(address);
        }

        public void RemoveConnectedFriends(IEnumerable<string> addresses)
        {
            foreach (string address in addresses)
                connectedFriends.Remove(address);
        }

        public void AddConnectedNonFriends(IEnumerable<string> addresses)
        {
            foreach (string address in addresses)
                ConnectedNonFriend.Add(address);
        }

        public void RemoveConnectedNonFriends(IEnumerable<string> addresses)
        {
            foreach (string address in addresses)
                ConnectedNonFriend.Remove(address);
        }

        public void AddUsersUnavailableToChat(IEnumerable<string> addresses)
        {
            foreach (string address in addresses)
                usersUnavailableToChat.Add(address);
        }

        public void RemoveUsersUnavailableToChat(IEnumerable<string> addresses)
        {
            foreach (string address in addresses)
                usersUnavailableToChat.Remove(address);
        }

        public bool IsFriendConnected(string userAddress) =>
            connectedFriends.Contains(userAddress);

        public bool IsNonFriendConnected(string userAddress) =>
            ConnectedNonFriend.Contains(userAddress);

        public bool IsBlockedUserConnected(string userAddress) =>
            connectedBlockedUsers.Contains(userAddress);

        public void ClearAll()
        {
            connectedFriends.Clear();
            ConnectedNonFriend.Clear();
            usersUnavailableToChat.Clear();
        }

        public bool IsUserConnected(string userAddress) =>
            connectedFriends.Contains(userAddress) || ConnectedNonFriend.Contains(userAddress);

        public bool IsUserUnavailableToChat(string userAddress) =>
            usersUnavailableToChat.Contains(userAddress);
    }
}
