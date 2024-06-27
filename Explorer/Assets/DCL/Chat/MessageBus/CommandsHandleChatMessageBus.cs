using Cysharp.Threading.Tasks;
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

        public event Action<ChatMessage>? OnMessageAdded;
        public event Action<string>? MessageSent;

        public CommandsHandleChatMessageBus(IChatMessagesBus origin, IReadOnlyDictionary<Regex, Func<IChatCommand>> commandsFactory)
        {
            this.origin = origin;
            this.chatCommandsHandler = new ChatCommandsHandler(commandsFactory);
            origin.OnMessageAdded += OriginOnOnMessageAdded;
            origin.MessageSent += OnMessageSent;
        }

        public void Dispose()
        {
            origin.OnMessageAdded -= OriginOnOnMessageAdded;
            origin.MessageSent -= OnMessageSent;

            origin.Dispose();
            commandCts.SafeCancelAndDispose();
        }

        private void OnMessageSent(string message)
        {
            MessageSent?.Invoke(message);
        }

        public void Send(string message)
        {
            if (chatCommandsHandler.TryGetChatCommand(message, ref commandTuple))
            {
                ExecuteChatCommandAsync(commandTuple.command, commandTuple.param).Forget();
                return;
            }

            if (chatCommandsHandler.StartsLikeCommand(message))
            {
                SendFromSystem($"🔴 Command not found: '{message}'");
                return;
            }

            origin.Send(message);
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
            OnMessageAdded?.Invoke(ChatMessage.NewFromSystem(message));
        }

        private void OriginOnOnMessageAdded(ChatMessage obj)
        {
            OnMessageAdded?.Invoke(obj);
        }
    }
}
