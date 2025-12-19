using Cysharp.Threading.Tasks;
using DCL.Chat.ChatServices;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using System.Threading;

namespace DCL.Chat.Commands
{
    public class SpamChatCommand : IChatCommand
    {
        public string Command => "spam";
        public string Description => "<b>/spam <i><count> <delayMs></i></b>\n  Sends <count> messages to the current channel with <delayMs> milliseconds between each message";

        private readonly IChatMessagesBus chatMessagesBus;
        private readonly CurrentChannelService currentChannelService;

        public SpamChatCommand(IChatMessagesBus chatMessagesBus, CurrentChannelService currentChannelService)
        {
            this.chatMessagesBus = chatMessagesBus;
            this.currentChannelService = currentChannelService;
        }

        public bool ValidateParameters(string[] parameters) =>
            parameters.Length == 2;

        public async UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            if (!int.TryParse(parameters[0], out int count) || count <= 0)
                return "🔴 Invalid count parameter. Must be a positive integer.";

            if (!int.TryParse(parameters[1], out int delayMs) || delayMs < 0)
                return "🔴 Invalid delay parameter. Must be a non-negative integer.";

            SendSpamMessagesAsync(count, delayMs, ct).Forget();

            return $"🟢 Sending {count} messages with {delayMs}ms delay between each...";
        }

        private async UniTaskVoid SendSpamMessagesAsync(int count, int delayMs, CancellationToken ct)
        {
            ChatChannel channel = currentChannelService.CurrentChannel;

            for (var i = 0; i < count; i++)
            {
                if (ct.IsCancellationRequested)
                    break;

                chatMessagesBus.SendWithUtcNowTimestamp(
                    channel,
                    $"Spam message {i + 1}/{count}",
                    ChatMessageOrigin.CHAT);

                if (i < count - 1 && delayMs > 0)
                    await UniTask.Delay(delayMs, cancellationToken: ct);
            }
        }
    }
}



