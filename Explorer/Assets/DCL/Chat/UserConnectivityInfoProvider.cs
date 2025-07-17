using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Communities;
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
using Utility.Types;

namespace DCL.Chat
{
    internal class UserConnectivityInfoProvider : IDisposable
    {
        public delegate void UserConnectedDelegate(string userAddress, ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType);
        public delegate void UserDisconnectedDelegate(string userAddress, ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType);
        public delegate void ConversationInitializedDelegate(ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType);

        public event UserConnectedDelegate? UserConnected;
        public event UserDisconnectedDelegate? UserDisconnected;
        public event ConversationInitializedDelegate? ConversationInitialized;

        private readonly IRoom islandRoom;
        private readonly IRoom chatRoom;
        private readonly CommunitiesEventBus communitiesEventBus;
        private readonly IChatHistory chatHistory;
        private readonly IRealmNavigator realmNavigator;

        private readonly Dictionary<ChatChannel.ChannelId, HashSet<string>> participantsPerChannel = new Dictionary<ChatChannel.ChannelId, HashSet<string>>();
        private readonly ChatChannel.ChannelId privateConversationOnlineUserListId = new ChatChannel.ChannelId("OnlinePrivateConversations");

        public UserConnectivityInfoProvider(IRoom islandRoom, IRoom chatRoom, CommunitiesEventBus communitiesEventBus, IChatHistory chatHistory, IRealmNavigator realmNavigator)
        {
            this.islandRoom = islandRoom;
            this.chatRoom = chatRoom;
            this.chatHistory = chatHistory;
            this.communitiesEventBus = communitiesEventBus;
            this.realmNavigator = realmNavigator;
        }

        public void Dispose()
        {
            islandRoom.Participants.UpdatesFromParticipant -= OnIslandRoomUpdatesFromParticipant;
            islandRoom.ConnectionStateChanged -= OnIslandRoomConnectionStateChanged;
            chatRoom.Participants.UpdatesFromParticipant -= OnChatRoomUpdatesFromParticipantAsync;
            chatRoom.ConnectionStateChanged -= OnChatRoomConnectionStateChanged;
            communitiesEventBus.UserConnectedToCommunity -= OnCommunitiesEventBusUserConnectedToCommunity;
            communitiesEventBus.UserDisconnectedFromCommunity -= OnCommunitiesEventBusUserDisconnectedToCommunity;
            realmNavigator.NavigationExecuted -= OnRealmNavigatorNavigationExecuted;
        }

        public void Initialize(bool isCommunityEnabled, CommunitiesDataProvider communitiesDataProvider)
        {
            islandRoom.Participants.UpdatesFromParticipant += OnIslandRoomUpdatesFromParticipant;
            islandRoom.ConnectionStateChanged += OnIslandRoomConnectionStateChanged;
            chatRoom.Participants.UpdatesFromParticipant += OnChatRoomUpdatesFromParticipantAsync;
            chatRoom.ConnectionStateChanged += OnChatRoomConnectionStateChanged;
            realmNavigator.NavigationExecuted += OnRealmNavigatorNavigationExecuted;

            if (isCommunityEnabled)
            {
                communitiesEventBus.UserConnectedToCommunity += OnCommunitiesEventBusUserConnectedToCommunity;
                communitiesEventBus.UserDisconnectedFromCommunity += OnCommunitiesEventBusUserDisconnectedToCommunity;

                foreach (KeyValuePair<ChatChannel.ChannelId, ChatChannel> channel in chatHistory.Channels)
                {
                    if (channel.Value.ChannelType == ChatChannel.ChatChannelType.COMMUNITY)
                        InitializeOnlineChannelParticipantsInCommunitiesAsync(channel.Key, communitiesDataProvider, CancellationToken.None).Forget(); //TODO
                }
            }

            // Livekit connection may happen before the chat loads, so they have to be initialized here;
            // otherwise they will be initialized later the event arrives
            if (islandRoom.Info.ConnectionState == ConnectionState.ConnConnected)
                OnIslandRoomConnectionStateChanged(ConnectionState.ConnConnected);

            if (chatRoom.Info.ConnectionState == ConnectionState.ConnConnected)
                OnChatRoomConnectionStateChanged(ConnectionState.ConnConnected);
        }

        private void OnRealmNavigatorNavigationExecuted(Vector2Int obj)
        {
            OnIslandRoomConnectionStateChanged(ConnectionState.ConnConnected);
        }

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

        public void RemoveConversation(ChatChannel.ChannelId removedChannelId, ChatChannel.ChatChannelType channelType)
        {
            if (channelType is ChatChannel.ChatChannelType.NEARBY or ChatChannel.ChatChannelType.COMMUNITY)
                participantsPerChannel.Remove(removedChannelId);
        }

        public HashSet<string> GetOnlineUsersInConversation(ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType)
        {
            if (channelType == ChatChannel.ChatChannelType.USER)
                return participantsPerChannel[privateConversationOnlineUserListId];
            else
                return participantsPerChannel[channelId];
        }

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
                    Debug.Log("#PARTICIPANT: " + memberData.memberAddress + ", " + channelId.Id);
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
            Debug.Log("+PARTICIPANT: " + userConnectivity.Member.Address + ", " + userConnectivity.CommunityId + " " + userConnectivity.Status);
            ChatChannel.ChannelId communityChannelId = ChatChannel.NewCommunityChannelId(userConnectivity.CommunityId);;

            participantsPerChannel[communityChannelId].Add(userConnectivity.Member.Address);

            UserConnected?.Invoke(userConnectivity.Member.Address, communityChannelId, ChatChannel.ChatChannelType.COMMUNITY);
        }

        private void OnCommunitiesEventBusUserDisconnectedToCommunity(CommunityMemberConnectivityUpdate userConnectivity)
        {
            Debug.Log("-PARTICIPANT: " + userConnectivity.Member.Address + ", " + userConnectivity.CommunityId + " " + userConnectivity.Status);
            ChatChannel.ChannelId communityChannelId = ChatChannel.NewCommunityChannelId(userConnectivity.CommunityId);;

            participantsPerChannel[communityChannelId].Remove(userConnectivity.Member.Address);

            UserDisconnected?.Invoke(userConnectivity.Member.Address, communityChannelId, ChatChannel.ChatChannelType.COMMUNITY);
        }

        private void OnIslandRoomConnectionStateChanged(ConnectionState connectionState)
        {
            if (connectionState == ConnectionState.ConnConnected)
            {
                islandRoom.ConnectionStateChanged -= OnIslandRoomConnectionStateChanged;

                IReadOnlyCollection<string> roomParticipants = islandRoom.Participants.RemoteParticipantIdentities();
                Debug.Log("#PARTICIPANT: NEARBY CLEARED");
                participantsPerChannel[ChatChannel.NEARBY_CHANNEL_ID].Clear();

                foreach (string roomParticipant in roomParticipants)
                {
                    Debug.Log("#PARTICIPANT: " + roomParticipant);
                    participantsPerChannel[ChatChannel.NEARBY_CHANNEL_ID].Add(roomParticipant);
                }

                ConversationInitialized?.Invoke(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY);
            }
        }

        private void OnIslandRoomUpdatesFromParticipant(Participant participant, UpdateFromParticipant update)
        {
            if (update == UpdateFromParticipant.Connected)
            {
                Debug.Log("+PARTICIPANT: " + participant.Identity + " " + update);
                participantsPerChannel[ChatChannel.NEARBY_CHANNEL_ID].Add(participant.Identity);
                UserConnected?.Invoke(participant.Identity, ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY);
            }
            else if (update == UpdateFromParticipant.Disconnected)
            {
                Debug.Log("-PARTICIPANT: " + participant.Identity + " " + update);
                participantsPerChannel[ChatChannel.NEARBY_CHANNEL_ID].Remove(participant.Identity);
                UserDisconnected?.Invoke(participant.Identity, ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY);
            }
        }

        private void OnChatRoomConnectionStateChanged(ConnectionState connectionState)
        {
            if (connectionState == ConnectionState.ConnConnected)
            {
                chatRoom.ConnectionStateChanged -= OnChatRoomConnectionStateChanged;

                if(!participantsPerChannel.ContainsKey(privateConversationOnlineUserListId))
                   participantsPerChannel.Add(privateConversationOnlineUserListId, new HashSet<string>());

                IReadOnlyCollection<string> roomParticipants = chatRoom.Participants.RemoteParticipantIdentities();
                participantsPerChannel[privateConversationOnlineUserListId].Clear();

                // Checks that the participants have an open conversation with the local user
                foreach (KeyValuePair<ChatChannel.ChannelId, ChatChannel> chatChannel in chatHistory.Channels)
                {
                    foreach (string roomParticipant in roomParticipants)
                    {
                        if (chatChannel.Value.ChannelType == ChatChannel.ChatChannelType.USER && chatChannel.Key.Id == roomParticipant)
                        {
                            Debug.Log("#PARTICIPANT: " + roomParticipant);
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
                    Debug.Log("+PARTICIPANT: " + participant.Identity + " " + update);
                    participantsPerChannel[privateConversationOnlineUserListId].Add(participant.Identity);
                    UserConnected?.Invoke(participant.Identity, channelId, ChatChannel.ChatChannelType.USER);
                }

            }
            else if (update == UpdateFromParticipant.Disconnected)
            {
                if (chatHistory.Channels.ContainsKey(channelId))
                {
                    Debug.Log("-PARTICIPANT: " + participant.Identity + " " + update);
                    participantsPerChannel[privateConversationOnlineUserListId].Remove(participant.Identity);
                    UserDisconnected?.Invoke(participant.Identity, channelId, ChatChannel.ChatChannelType.USER);
                }
            }
        }
    }
}
