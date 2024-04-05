using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DCL.Chat
{
    internal class ChatCommandsHandler
    {
        private const char CHAT_COMMAND_CHAR = '/';

        private readonly Dictionary<Regex, IChatCommand> commandsCache = new ();
        private readonly Dictionary<Regex, Func<IChatCommand>> commandsFactory;

        public ChatCommandsHandler(Dictionary<Regex, Func<IChatCommand>> commandsFactory)
        {
            this.commandsFactory = commandsFactory;
        }

        public bool TryGetChatCommand(in string message, ref IChatCommand command)
        {
            if (!message.StartsWith(CHAT_COMMAND_CHAR)) return false;

            foreach (Regex? commandRegex in commandsFactory.Keys)
            {
                Match match = commandRegex.Match(message);
                if (!match.Success) continue;

                if (!commandsCache.TryGetValue(commandRegex, out command))
                {
                    command = commandsFactory[commandRegex]();
                    commandsCache[commandRegex] = command;
                }

                command.Set(match);
                return true;
            }

            return false;
        }
    }
}
