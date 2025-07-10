using Arch.Core;
using DCL.Chat;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Friends;
using DCL.Friends.UserBlocking;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Nametags;
using DCL.Profiles;
using DCL.Settings.Settings;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DCL.Web3.Identities;
using MVC;

public interface IChatPresenterFactory
{
    ChatChannelsPresenter CreateConversationList(IChatChannelsView view, ChatConfig config);
    ChatMessageFeedPresenter CreateMessageFeed(IChatMessageFeedView view);
    ChatInputPresenter CreateChatInput(IChatInputView view);
    ChatTitlebarPresenter CreateTitlebar(IChatTitlebarView view);
    ChatMemberListPresenter CreateMemberList(IChatMemberListView view);

    // ChatInWorldBubblesPresenter CreateInWorldBubbles();
}

public class ChatPresenterFactory : IChatPresenterFactory
{
    private readonly IChatHistory chatHistory;
    private readonly IChatMessagesBus chatMessagesBus;
    private readonly IChatUserStateEventBus chatUserStateEventBus;
    private readonly IChatEventBus chatEventBus;
    private readonly IProfileCache profileCache;
    private readonly IWeb3IdentityCache web3IdentityCache;
    private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
    private readonly IRoomHub roomHub;
    private readonly World world;
    private readonly ViewDependencies viewDependencies;
    private readonly ChatSettingsAsset chatSettings;
    private readonly NametagsData nametagsData;
    private readonly ObjectProxy<IFriendsService> friendsServiceProxy;
    private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;
    private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
    private readonly ChatUserStateUpdater chatUserStateUpdater;
    private readonly RPCChatPrivacyService chatPrivacyService;
    private readonly IFriendsEventBus friendsEventBus;
    private readonly ObjectProxy<IFriendsService> friendsService;
    private readonly ChatService chatService;
    private readonly ChatMemberListService chatMemberListService;

    public ChatPresenterFactory(
        IChatHistory chatHistory,
        IChatMessagesBus chatMessagesBus,
        IChatEventBus chatEventBus,
        IProfileCache profileCache,
        IWeb3IdentityCache web3IdentityCache,
        IReadOnlyEntityParticipantTable entityParticipantTable,
        IRoomHub roomHub,
        World world,
        ChatSettingsAsset chatSettings,
        NametagsData nametagsData,
        ObjectProxy<IFriendsService> friendsServiceProxy,
        ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
        ProfileRepositoryWrapper profileRepositoryWrapper,
        RPCChatPrivacyService rpcChatPrivacyService,
        ChatUserStateUpdater userStateUpdater,
        IFriendsEventBus friendsEventBus,
        ObjectProxy<IFriendsService> friendsService,
        ChatService chatService,
        ChatMemberListService chatMemberListService)
    {
        this.chatHistory = chatHistory;
        this.chatMessagesBus = chatMessagesBus;
        this.chatEventBus = chatEventBus;
        this.profileCache = profileCache;
        this.web3IdentityCache = web3IdentityCache;
        this.entityParticipantTable = entityParticipantTable;
        this.roomHub = roomHub;
        this.world = world;
        this.chatSettings = chatSettings;
        this.nametagsData = nametagsData;
        this.friendsServiceProxy = friendsServiceProxy;
        this.userBlockingCacheProxy = userBlockingCacheProxy;
        this.profileRepositoryWrapper = profileRepositoryWrapper;
        this.chatPrivacyService = rpcChatPrivacyService;
        this.friendsEventBus = friendsEventBus;
        this.friendsService = friendsService;
        chatUserStateUpdater = userStateUpdater;
        this.chatService = chatService;
        this.chatMemberListService = chatMemberListService;
        
        chatUserStateEventBus = new ChatUserStateEventBus();
        var chatRoom = roomHub.ChatRoom();
        chatUserStateUpdater = new ChatUserStateUpdater(
            userBlockingCacheProxy,
            chatRoom.Participants,
            chatSettings,
            chatPrivacyService,
            chatUserStateEventBus,
            friendsEventBus,
            chatRoom,
            friendsService);
    }

    public ChatChannelsPresenter CreateConversationList(IChatChannelsView view, ChatConfig config)
    {
        return new ChatChannelsPresenter(view,
            chatHistory,
            chatUserStateEventBus,
            profileCache,
            profileRepositoryWrapper,
            config);
    }

    public ChatMessageFeedPresenter CreateMessageFeed(IChatMessageFeedView view)
    {
        return new ChatMessageFeedPresenter(view, chatHistory, profileCache, web3IdentityCache);
    }

    public ChatInputPresenter CreateChatInput(IChatInputView view)
    {
        return new ChatInputPresenter(view, chatEventBus,chatUserStateUpdater);
    }

    public ChatTitlebarPresenter CreateTitlebar(IChatTitlebarView view)
    {
        return new ChatTitlebarPresenter(view, roomHub, profileCache, profileRepositoryWrapper);
    }

    public ChatMemberListPresenter CreateMemberList(IChatMemberListView view)
    {
        return new ChatMemberListPresenter(view, roomHub, profileCache, profileRepositoryWrapper);
    }

    // public ChatInWorldBubblesPresenter CreateInWorldBubbles()
    // {
    //     return new ChatInWorldBubblesPresenter(chatMessagesBus, world, entityParticipantTable, profileCache, nametagsData, chatSettings);
    // }
}