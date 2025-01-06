using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
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
        private readonly Dictionary<string, IChatCommand> commands;
        private CancellationTokenSource commandCts = new ();

        public event Action<ChatMessage>? MessageAdded;

        public CommandsHandleChatMessageBus(IChatMessagesBus origin, IReadOnlyList<IChatCommand> commands)
        {
            this.origin = origin;
            this.commands = commands.ToDictionary(cmd => cmd.Command);
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
            if (message[0] == '/') // User tried running a command
            {
                HandleChatCommandAsync(message).Forget();
                return;
            }

            this.origin.Send(message, origin);
        }

        private async UniTaskVoid HandleChatCommandAsync(string message)
        {
            string[] split = message.Split(' ');
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
                        SendFromSystem(response);
                    }
                    catch (Exception) { SendFromSystem("🔴 Error running command."); }

                    return;
                }

                SendFromSystem($"🔴 Invalid parameters, usage:\n{command.Description}");
                return;
            }

            // Command not found
            SendFromSystem("🔴 Command not found.");
        }

        private void SendFromSystem(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            MessageAdded?.Invoke(ChatMessage.NewFromSystem(message));
        }

        private void OriginOnOnMessageAdded(ChatMessage obj)
        {
            MessageAdded?.Invoke(obj);
        }
    }
}
