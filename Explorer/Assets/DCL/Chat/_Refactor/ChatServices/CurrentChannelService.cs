using Cysharp.Threading.Tasks;
using DCL.Chat.ChatUseCases;
using DCL.Chat.History;
using System.Threading;
using Utility.Types;

namespace DCL.Chat.Services
{
    public interface ICurrentChannelService
    {
        ChatChannel? CurrentChannel { get; }
        ChatChannel.ChatChannelType? CurrentChannelType => CurrentChannel?.ChannelType;
        ChatChannel.ChannelId CurrentChannelId { get; }

        Result<ChatUserStateUpdater.ChatUserState> InputState { get; }

        void SetCurrentChannel(ChatChannel newChannel);

        UniTask<Result<ChatUserStateUpdater.ChatUserState>> ResolveInputStateAsync(CancellationToken ct);
    }

    public class CurrentChannelService : ICurrentChannelService
    {
        private readonly GetUserChatStatusCommand getUserChatStatusCommand;

        public CurrentChannelService(GetUserChatStatusCommand getUserChatStatusCommand)
        {
            this.getUserChatStatusCommand = getUserChatStatusCommand;
        }

        public Result<ChatUserStateUpdater.ChatUserState> InputState { get; private set; }

        public ChatChannel? CurrentChannel { get; private set; }
        public ChatChannel.ChannelId CurrentChannelId => CurrentChannel?.Id ?? default;

        public void SetCurrentChannel(ChatChannel newChannel)
        {
            CurrentChannel = newChannel;
        }

        public async UniTask<Result<ChatUserStateUpdater.ChatUserState>> ResolveInputStateAsync(CancellationToken ct)
        {
            if (CurrentChannel == null)
                return InputState = Result<ChatUserStateUpdater.ChatUserState>.ErrorResult("No channel selected");

            switch (CurrentChannel.ChannelType)
            {
                case ChatChannel.ChatChannelType.NEARBY:
                    return InputState = Result<ChatUserStateUpdater.ChatUserState>.SuccessResult(ChatUserStateUpdater.ChatUserState.CONNECTED);
                case ChatChannel.ChatChannelType.USER:
                {
                    ChatUserStateUpdater.ChatUserState status = await getUserChatStatusCommand.ExecuteAsync(CurrentChannel.Id.Id, ct);

                    return InputState = ct.IsCancellationRequested
                        ? Result<ChatUserStateUpdater.ChatUserState>.CancelledResult()
                        : Result<ChatUserStateUpdater.ChatUserState>.SuccessResult(status);
                }
                default:
                    return InputState = Result<ChatUserStateUpdater.ChatUserState>.ErrorResult($"{CurrentChannel.ChannelType} is not supported for input state resolution");
            }
        }

        public void Clear()
        {
            CurrentChannel = null;
        }
    }
}


