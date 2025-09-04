using Cysharp.Threading.Tasks;
using DCL.Chat.ChatServices;
using DCL.VoiceChat;
using System.Threading;

namespace DCL.Chat.ChatCommands
{
    public class GetUserCallStatusCommand
    {
        private readonly GetUserChatStatusCommand getUserChatStatusCommand;

        public GetUserCallStatusCommand(GetUserChatStatusCommand getUserChatStatusCommand)
        {
            this.getUserChatStatusCommand = getUserChatStatusCommand;
        }

        public async UniTask<CallButtonController.OtherUserCallStatus> ExecuteAsync(string id, CancellationToken ct)
        {
            var result = await getUserChatStatusCommand.ExecuteAsync(id, ct);
            switch (result)
            {
                case PrivateConversationUserStateService.ChatUserState.CONNECTED:
                    return CallButtonController.OtherUserCallStatus.USER_AVAILABLE;
                case PrivateConversationUserStateService.ChatUserState.PRIVATE_MESSAGES_BLOCKED_BY_OWN_USER:
                    return CallButtonController.OtherUserCallStatus.OWN_USER_REJECTS_CALLS;
                case PrivateConversationUserStateService.ChatUserState.PRIVATE_MESSAGES_BLOCKED:
                    return CallButtonController.OtherUserCallStatus.USER_REJECTS_CALLS;
                case PrivateConversationUserStateService.ChatUserState.BLOCKED_BY_OWN_USER:
                case PrivateConversationUserStateService.ChatUserState.DISCONNECTED:
                default: return CallButtonController.OtherUserCallStatus.USER_OFFLINE;
            }
        }
    }
}
