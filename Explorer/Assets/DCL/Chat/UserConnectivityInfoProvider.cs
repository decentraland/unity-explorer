using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Communities;
using DCL.Communities.CommunitiesDataProvider;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using Decentraland.SocialService.V2;
using ECS.SceneLifeCycle.Realm;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Types;

namespace DCL.Chat
{
    /// <summary>
    /// Once initialized, it keeps track of who is reachable in each chat conversation for the local user.
    /// It notifies when a user is connected or disconnected in a conversation, which does not mean to be connected or disconnected from DCL.
    /// For example, leaving a community will mark a user as disconnected.
    /// </summary>
    internal class UserConnectivityInfoProvider : IDisposable
    {
        public delegate void UserConnectedDelegate(string userAddress, ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType);
        public delegate void UserDisconnectedDelegate(string userAddress, ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType);
        public delegate void ConversationInitializedDelegate(ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType);

        /// <summary>
        /// Raised when a user is connected to a conversation (joined a community, teleported to a place nearby...).
        /// </summary>
        public event UserConnectedDelegate? UserConnected;

        /// <summary>
        /// Raised when a user is connected to a conversation (left a community, teleported to a far place...).
        /// </summary>
        public event UserDisconnectedDelegate? UserDisconnected;

        /// <summary>
        /// Raised when the list of online users in a conversation has been rebuilt.
        /// </summary>
        public event ConversationInitializedDelegate? ConversationInitialized;

        private readonly IRoom islandRoom;
        private readonly IRoom chatRoom;
        private readonly CommunitiesEventBus communitiesEventBus;
        private readonly IChatHistory chatHistory;
        private readonly IRealmNavigator realmNavigator;

        // Stores a list of wallet addreses of reachable users per channel.
        // In the case of private channels, there is one special channelId that contains all the online users for which the local user
        // as an open conversation.
        private readonly Dictionary<ChatChannel.ChannelId, HashSet<string>> participantsPerChannel = new Dictionary<ChatChannel.ChannelId, HashSet<string>>();
        private readonly ChatChannel.ChannelId privateConversationOnlineUserListId = new ChatChannel.ChannelId("OnlinePrivateConversations");

        private readonly CancellationTokenSource initializationCts = new CancellationTokenSource();

        public UserConnectivityInfoProvider(IRoom islandRoom, IRoom chatRoom, CommunitiesEventBus communitiesEventBus, IChatHistory chatHistory, IRealmNavigator realmNavigator)
        {
            this.islandRoom = islandRoom;
            this.chatRoom = chatRoom;
            this.chatHistory = chatHistory;
            this.communitiesEventBus = communitiesEventBus;
            this.realmNavigator = realmNavigator;

            participantsPerChannel.Add(privateConversationOnlineUserListId, new HashSet<string>());
        }

        public void Dispose()
        {
            islandRoom.Participants.UpdatesFromParticipant -= OnIslandRoomUpdatesFromParticipantAsync;
            islandRoom.ConnectionStateChanged -= OnIslandRoomConnectionStateChangedAsync;
            chatRoom.Participants.UpdatesFromParticipant -= OnChatRoomUpdatesFromParticipantAsync;
            chatRoom.ConnectionStateChanged -= OnChatRoomConnectionStateChangedAsync;
            communitiesEventBus.UserConnectedToCommunity -= OnCommunitiesEventBusUserConnectedToCommunity;
            communitiesEventBus.UserDisconnectedFromCommunity -= OnCommunitiesEventBusUserDisconnectedFromCommunity;
            realmNavigator.NavigationExecuted -= OnRealmNavigatorNavigationExecuted;
            initializationCts.SafeCancelAndDispose();
        }

        /// <summary>
        /// Starts gatherting information about the connected users in all the open conversations.
        /// </summary>
        /// <param name="isCommunityEnabled">Whether the communities feature is used or not.</param>
        /// <param name="communitiesDataProvider">A data source for obtaining the community conversations of the user.</param>
        public void Initialize(bool isCommunityEnabled, CommunitiesDataProvider communitiesDataProvider)
        {
            islandRoom.Participants.UpdatesFromParticipant += OnIslandRoomUpdatesFromParticipantAsync;
            islandRoom.ConnectionStateChanged += OnIslandRoomConnectionStateChangedAsync;
            chatRoom.Participants.UpdatesFromParticipant += OnChatRoomUpdatesFromParticipantAsync;
            chatRoom.ConnectionStateChanged += OnChatRoomConnectionStateChangedAsync;
            realmNavigator.NavigationExecuted += OnRealmNavigatorNavigationExecuted;

            if (isCommunityEnabled)
            {
                communitiesEventBus.UserConnectedToCommunity += OnCommunitiesEventBusUserConnectedToCommunity;
                communitiesEventBus.UserDisconnectedFromCommunity += OnCommunitiesEventBusUserDisconnectedFromCommunity;

                foreach (KeyValuePair<ChatChannel.ChannelId, ChatChannel> channel in chatHistory.Channels)
                {
                    if (channel.Value.ChannelType == ChatChannel.ChatChannelType.COMMUNITY)
                        InitializeOnlineChannelParticipantsInCommunitiesAsync(channel.Key, communitiesDataProvider, initializationCts.Token).Forget();
                }
            }

            // Livekit connection may happen before the chat loads, so they have to be initialized here;
            // otherwise they will be initialized later the event arrives
            if (islandRoom.Info.ConnectionState == ConnectionState.ConnConnected)
                OnIslandRoomConnectionStateChangedAsync(ConnectionState.ConnConnected);

            if (chatRoom.Info.ConnectionState == ConnectionState.ConnConnected)
                OnChatRoomConnectionStateChangedAsync(ConnectionState.ConnConnected);
        }

        /// <summary>
        /// Adds a conversation for which to track its connected users.
        /// </summary>
        /// <param name="addedChannelId">The id of the conversation.</param>
        /// <param name="channelType">The type of conversation.</param>
        public void AddConversation(ChatChannel.ChannelId addedChannelId, ChatChannel.ChatChannelType channelType)
        {
            if (channelType == ChatChannel.ChatChannelType.USER)
            {
                if(chatRoom.Participants.RemoteParticipant(addedChannelId.Id) != null)
                    participantsPerChannel[privateConversationOnlineUserListId].Add(addedChannelId.Id);
            }
            else
            {
                participantsPerChannel.Add(addedChannelId, new HashSet<string>());
            }
        }

        /// <summary>
        /// Stops tracking the connectivity of the users for a conversation.
        /// </summary>
        /// <param name="removedChannelId">The id of the conversation.</param>
        /// <param name="channelType">The type of the conversation.</param>
        public void RemoveConversation(ChatChannel.ChannelId removedChannelId, ChatChannel.ChatChannelType channelType)
        {
            if (channelType is ChatChannel.ChatChannelType.NEARBY or ChatChannel.ChatChannelType.COMMUNITY)
                participantsPerChannel.Remove(removedChannelId);
        }

        /// <summary>
        /// Gets a list of wallet addreses of all the users that are connected to a conversation.
        /// </summary>
        /// <param name="channelId">The id of the conversation.</param>
        /// <param name="channelType">The type of conversation.</param>
        /// <returns>A list of unique addresses. If a user is not present, it is offline.</returns>
        public HashSet<string> GetOnlineUsersInConversation(ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType)
        {
            if (channelType == ChatChannel.ChatChannelType.USER)
                return participantsPerChannel[privateConversationOnlineUserListId];
            else
                return participantsPerChannel[channelId];
        }

        /// <summary>
        /// Gets whether a conversation has been previously added.
        /// </summary>
        /// <param name="channelId">The id of the conversation.</param>
        /// <param name="channelType">The type of the conversation.</param>
        /// <returns>True if the conversation was added; False otherwise.</returns>
        public bool HasConversation(ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType)
        {
            if (channelType == ChatChannel.ChatChannelType.USER)
                return participantsPerChannel[privateConversationOnlineUserListId].Contains(channelId.Id);
            else
                return participantsPerChannel.ContainsKey(channelId);
        }

        // Note: For Nearby and private conversations, online channels are initialized when their rooms connect
        private async UniTaskVoid InitializeOnlineChannelParticipantsInCommunitiesAsync(ChatChannel.ChannelId channelId, CommunitiesDataProvider communitiesDataProvider, CancellationToken ct)
        {
            Result<GetCommunityMembersResponse> result = await communitiesDataProvider.GetOnlineCommunityMembersAsync(ChatChannel.GetCommunityIdFromChannelId(channelId), ct).SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (result.Success)
            {
                GetCommunityMembersResponse response = result.Value;

                foreach (GetCommunityMembersResponse.MemberData memberData in response.data.results)
                {
                    ReportHub.Log(ReportCategory.DEBUG, $"#PARTICIPANT: {memberData.memberAddress}, {channelId.Id}");
                    participantsPerChannel[channelId].Add(memberData.memberAddress);
                }

                ConversationInitialized?.Invoke(channelId, ChatChannel.ChatChannelType.COMMUNITY);
            }
            else
            {
                // TODO
            }
        }

        private void OnCommunitiesEventBusUserConnectedToCommunity(CommunityMemberConnectivityUpdate userConnectivity)
        {
            ReportHub.Log(ReportCategory.DEBUG, $"+PARTICIPANT: {userConnectivity.Member.Address}, {userConnectivity.CommunityId}, {userConnectivity.Status}");
            ChatChannel.ChannelId communityChannelId = ChatChannel.NewCommunityChannelId(userConnectivity.CommunityId);;

            participantsPerChannel[communityChannelId].Add(userConnectivity.Member.Address);

            UserConnected?.Invoke(userConnectivity.Member.Address, communityChannelId, ChatChannel.ChatChannelType.COMMUNITY);
        }

        private void OnCommunitiesEventBusUserDisconnectedFromCommunity(CommunityMemberConnectivityUpdate userConnectivity)
        {
            ReportHub.Log(ReportCategory.DEBUG, $"-PARTICIPANT: {userConnectivity.Member.Address}, {userConnectivity.CommunityId}, {userConnectivity.Status}");
            ChatChannel.ChannelId communityChannelId = ChatChannel.NewCommunityChannelId(userConnectivity.CommunityId);;

            participantsPerChannel[communityChannelId].Remove(userConnectivity.Member.Address);

            UserDisconnected?.Invoke(userConnectivity.Member.Address, communityChannelId, ChatChannel.ChatChannelType.COMMUNITY);
        }

        private async void OnIslandRoomConnectionStateChangedAsync(ConnectionState connectionState)
        {
            await UniTask.SwitchToMainThread();

            if (connectionState == ConnectionState.ConnConnected)
            {
                islandRoom.ConnectionStateChanged -= OnIslandRoomConnectionStateChangedAsync;

                IReadOnlyCollection<string> roomParticipants = islandRoom.Participants.RemoteParticipantIdentities();
                ReportHub.Log(ReportCategory.DEBUG, "#PARTICIPANT: NEARBY CLEARED");
                participantsPerChannel[ChatChannel.NEARBY_CHANNEL_ID].Clear();

                foreach (string roomParticipant in roomParticipants)
                {
                    ReportHub.Log(ReportCategory.DEBUG, $"#PARTICIPANT: {roomParticipant}");
                    participantsPerChannel[ChatChannel.NEARBY_CHANNEL_ID].Add(roomParticipant);
                }

                ConversationInitialized?.Invoke(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY);
            }
        }

        private async void OnIslandRoomUpdatesFromParticipantAsync(Participant participant, UpdateFromParticipant update)
        {
            await UniTask.SwitchToMainThread();

            if (update == UpdateFromParticipant.Connected)
            {
                ReportHub.Log(ReportCategory.DEBUG, $"+PARTICIPANT: {participant.Identity}, {update}");
                participantsPerChannel[ChatChannel.NEARBY_CHANNEL_ID].Add(participant.Identity);
                UserConnected?.Invoke(participant.Identity, ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY);
            }
            else if (update == UpdateFromParticipant.Disconnected)
            {
                ReportHub.Log(ReportCategory.DEBUG, $"-PARTICIPANT: {participant.Identity}, {update}");
                // Hotfix: Due to a problem with Livekit connection messages, greying out nearby messages is not working properly (connected users look like disconnected)
                //         So for now disconnections will be ignored in Nearby
                // participantsPerChannel[ChatChannel.NEARBY_CHANNEL_ID].Remove(participant.Identity);
                // UserDisconnected?.Invoke(participant.Identity, ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY);
            }
        }

        private async void OnChatRoomConnectionStateChangedAsync(ConnectionState connectionState)
        {
            await UniTask.SwitchToMainThread();

            if (connectionState == ConnectionState.ConnConnected)
            {
                chatRoom.ConnectionStateChanged -= OnChatRoomConnectionStateChangedAsync;

                IReadOnlyCollection<string> roomParticipants = chatRoom.Participants.RemoteParticipantIdentities();
                participantsPerChannel[privateConversationOnlineUserListId].Clear();

                // Checks that the participants have an open conversation with the local user
                foreach (KeyValuePair<ChatChannel.ChannelId, ChatChannel> chatChannel in chatHistory.Channels)
                {
                    foreach (string roomParticipant in roomParticipants)
                    {
                        if (chatChannel.Value.ChannelType == ChatChannel.ChatChannelType.USER && chatChannel.Key.Id == roomParticipant)
                        {
                            ReportHub.Log(ReportCategory.DEBUG, $"#PARTICIPANT: {roomParticipant}");
                            participantsPerChannel[privateConversationOnlineUserListId].Add(roomParticipant);
                            ConversationInitialized?.Invoke(chatChannel.Key, ChatChannel.ChatChannelType.USER);
                        }
                    }
                }
            }
        }

        private async void OnChatRoomUpdatesFromParticipantAsync(Participant participant, UpdateFromParticipant update)
        {
            await UniTask.SwitchToMainThread();

            ChatChannel.ChannelId channelId = new ChatChannel.ChannelId(participant.Identity);

            if (update == UpdateFromParticipant.Connected)
            {
                if (chatHistory.Channels.ContainsKey(channelId))
                {
                    ReportHub.Log(ReportCategory.DEBUG, $"+PARTICIPANT: {participant.Identity}, {update}");
                    participantsPerChannel[privateConversationOnlineUserListId].Add(participant.Identity);
                    UserConnected?.Invoke(participant.Identity, channelId, ChatChannel.ChatChannelType.USER);
                }

            }
            else if (update == UpdateFromParticipant.Disconnected)
            {
                if (chatHistory.Channels.ContainsKey(channelId))
                {
                    ReportHub.Log(ReportCategory.DEBUG, $"-PARTICIPANT: {participant.Identity}, {update}");
                    participantsPerChannel[privateConversationOnlineUserListId].Remove(participant.Identity);
                    UserDisconnected?.Invoke(participant.Identity, channelId, ChatChannel.ChatChannelType.USER);
                }
            }
        }

        // Called when the user teleports
        private void OnRealmNavigatorNavigationExecuted(Vector2Int obj)
        {
            OnIslandRoomConnectionStateChangedAsync(ConnectionState.ConnConnected);
        }
    }
}
