using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using DCL.Chat.History;
using DCL.RealmNavigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Utility;

namespace DCL.Chat.MessageBus
{
    public class CommandsHandleChatMessageBus : IChatMessagesBus
    {
        private readonly IChatMessagesBus origin;
        private readonly ILoadingStatus loadingStatus;
        private readonly Dictionary<string, IChatCommand> commands;
        private CancellationTokenSource commandCts = new ();

        public event Action<ChatChannel.ChannelId, ChatMessage>? MessageAdded;

        public CommandsHandleChatMessageBus(IChatMessagesBus origin, IReadOnlyList<IChatCommand> commands, ILoadingStatus loadingStatus)
        {
            this.origin = origin;
            this.loadingStatus = loadingStatus;
            this.commands = commands.ToDictionary(cmd => cmd.Command);
            origin.MessageAdded += OriginOnOnMessageAdded;
        }

        public void Dispose()
        {
            origin.MessageAdded -= OriginOnOnMessageAdded;

            origin.Dispose();
            commandCts.SafeCancelAndDispose();
        }

        public void Send(ChatChannel.ChannelId channelId, string message, string origin)
        {
            if (loadingStatus.CurrentStage.Value != LoadingStatus.LoadingStage.Completed)
                return;

            if (message[0] == '/') // User tried running a command
            {
                HandleChatCommandAsync(channelId, message).Forget();
                return;
            }

            this.origin.Send(channelId, message, origin);
        }

        private async UniTaskVoid HandleChatCommandAsync(ChatChannel.ChannelId channelId, string message)
        {
            string[] split = message.Replace(", ", ",").Split(' '); // Split by space but keep commas
            string userCommand = split[0][1..];
            string[] parameters = new ArraySegment<string>(split, 1, split.Length - 1).ToArray()!;

            if (commands.TryGetValue(userCommand, out IChatCommand? command))
            {
                if (command.ValidateParameters(parameters))
                {
                    // Command found and parameters validated, run it
                    commandCts = commandCts.SafeRestart();

                    try
                    {
                        string response = await command.ExecuteCommandAsync(parameters, commandCts.Token);
                        SendFromSystem(channelId, response);
                    }
                    catch (Exception) { SendFromSystem(channelId, "🔴 Error running command."); }

                    return;
                }

                SendFromSystem(channelId, $"🔴 Invalid parameters, usage:\n{command.Description}");
                return;
            }

            // Command not found
            SendFromSystem(channelId, "🔴 Command not found.");
        }

        private void SendFromSystem(ChatChannel.ChannelId channelId, string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            MessageAdded?.Invoke(channelId, ChatMessage.NewFromSystem(message));
        }

        private void OriginOnOnMessageAdded(ChatChannel.ChannelId channelId, ChatMessage obj)
        {
            MessageAdded?.Invoke(channelId, obj);
        }
    }
}
