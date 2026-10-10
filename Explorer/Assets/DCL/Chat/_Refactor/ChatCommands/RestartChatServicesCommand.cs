using Cysharp.Threading.Tasks;
using DCL.Chat.ChatServices;
using System.Threading;

namespace DCL.Chat.ChatCommands
{
    public class RestartChatServicesCommand
    {
        private readonly PrivateConversationUserStateService privateConversationUserStateService;
        private readonly CommunityUserStateService communityUserStateService;
        private readonly ChatMemberListService chatMemberListService;

        public RestartChatServicesCommand(
            PrivateConversationUserStateService privateConversationUserStateService,
            CommunityUserStateService communityUserStateService,
            ChatMemberListService chatMemberListService)
        {
            this.privateConversationUserStateService = privateConversationUserStateService;
            this.communityUserStateService = communityUserStateService;
            this.chatMemberListService = chatMemberListService;
        }

        public async UniTask ExecuteAsync(CancellationToken ct)
        {
            await privateConversationUserStateService.InitializeAsync(ct);

            communityUserStateService.SubscribeToEvents();

            chatMemberListService.Start();
        }
    }
}