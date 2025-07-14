#nullable enable
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

        public event Action<ChatChannel.ChannelId, ChatChannel.ChatChannelType, ChatMessage>? MessageAdded;

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

        public void Send(ChatChannel channel, string message, string origin, string topic)
        {
            if (loadingStatus.CurrentStage.Value != LoadingStatus.LoadingStage.Completed)
                return;

            if (message[0] == '/') // User tried running a command
            {
                //We send the results of the command to the nearby channel
                HandleChatCommandAsync(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY, message).Forget();
                return;
            }

            this.origin.Send(channel, message, origin, topic);
        }

        private async UniTaskVoid HandleChatCommandAsync(ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType, string message)
        {
            string[] split = message.Replace(", ", ",").Split(' '); // Split by space but keep commas
            string userCommand = split[0][1..];
            string[] parameters = new ArraySegment<string>(split, 1, split.Length - 1).ToArray()!;

            if (commands.TryGetValue(userCommand, out IChatCommand command))
            {
                if (command.ValidateParameters(parameters))
                {
                    // Command found and parameters validated, run it
                    commandCts = commandCts.SafeRestart();

                    try
                    {
                        string response = await command.ExecuteCommandAsync(parameters, commandCts.Token);
                        SendFromSystem(channelId, channelType, response);
                    }
                    catch (Exception) { SendFromSystem(channelId, channelType, "🔴 Error running command."); }

                    return;
                }

                SendFromSystem(channelId, channelType, $"🔴 Invalid parameters, usage:\n{command.Description}");
                return;
            }

            // Command not found
            SendFromSystem(channelId, channelType, "🔴 Command not found.");
        }

        private void SendFromSystem(ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType, string? message)
        {
            if (string.IsNullOrEmpty(message)) return;

            MessageAdded?.Invoke(channelId, channelType, ChatMessage.NewFromSystem(message));
        }

        private void OriginOnOnMessageAdded(ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType, ChatMessage obj)
        {
            MessageAdded?.Invoke(channelId, channelType, obj);
        }
    }
}
