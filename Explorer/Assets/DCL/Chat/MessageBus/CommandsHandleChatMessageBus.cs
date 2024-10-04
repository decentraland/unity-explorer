using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Utility;

namespace DCL.Chat.MessageBus
{
    public class CommandsHandleChatMessageBus : IChatMessagesBus
    {
        private readonly IChatMessagesBus origin;
        private readonly ChatCommandsHandler chatCommandsHandler;
        private CancellationTokenSource commandCts = new ();
        private (IChatCommand command, Match param) commandTuple;

        public event Action<ChatMessage>? MessageAdded;

        public CommandsHandleChatMessageBus(IChatMessagesBus origin, IReadOnlyDictionary<Regex, Func<IChatCommand>> commandsFactory)
        {
            this.origin = origin;
            this.chatCommandsHandler = new ChatCommandsHandler(commandsFactory);
            origin.MessageAdded += OriginOnOnMessageAdded;
        }

        public void Dispose()
        {
            origin.MessageAdded -= OriginOnOnMessageAdded;

            origin.Dispose();
            commandCts.SafeCancelAndDispose();
        }

        public void Send(string message, string origin)
        {
            //If the message doesn't start as a command (with "/"), we just forward it to the chat
            if (!ChatCommandsHandler.StartsLikeCommand(message))
            {
                this.origin.Send(message, origin);
                return;
            }

            if (chatCommandsHandler.TryGetChatCommand(message, ref commandTuple))
            {
                ExecuteChatCommandAsync(commandTuple.command, commandTuple.param).Forget();
                return;
            }

            SendFromSystem($"🔴 Command not found: '{message}'");
        }

        private async UniTask ExecuteChatCommandAsync(IChatCommand command, Match param)
        {
            commandCts = commandCts.SafeRestart();

            string? response = await command.ExecuteAsync(param, commandCts.Token);

            if (!string.IsNullOrEmpty(response))
                SendFromSystem(response);
        }

        private void SendFromSystem(string message)
        {
            MessageAdded?.Invoke(ChatMessage.NewFromSystem(message));
        }

        private void OriginOnOnMessageAdded(ChatMessage obj)
        {
            MessageAdded?.Invoke(obj);
        }
    }
}
