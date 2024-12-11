using DCL.Chat.Commands;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DCL.Chat
{
    public class ChatCommandsHandler
    {
        private const string CHAT_COMMAND_CHAR = "/";

        private readonly IReadOnlyList<IChatCommand> commands;

        public ChatCommandsHandler(IReadOnlyList<IChatCommand> commands)
        {
            this.commands = commands;
        }

        public bool TryGetChatCommand(in string message, ref (IChatCommand command, Match match) commandTuple)
        {
            foreach (IChatCommand cmd in commands)
            {
                commandTuple.match = cmd.Regex.Match(message);

                if (!commandTuple.match.Success) continue;

                commandTuple.command = cmd;

                return true;
            }

            return false;
        }

        public static bool StartsLikeCommand(string message) =>
            message
               .AsSpan()
               .Trim()
               .StartsWith(CHAT_COMMAND_CHAR);
    }
}
