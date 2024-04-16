using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DCL.Chat
{
    internal class ChatCommandsHandler
    {
        public const string CHAT_COMMAND_CHAR = "/";

        private readonly Dictionary<Regex, IChatCommand> commandsCache = new ();
        private readonly IReadOnlyDictionary<Regex, Func<IChatCommand>> commandsFactory;

        public ChatCommandsHandler(IReadOnlyDictionary<Regex, Func<IChatCommand>> commandsFactory)
        {
            this.commandsFactory = commandsFactory;
        }

        public bool TryGetChatCommand(in string message, ref (IChatCommand command, Match match) commandTuple)
        {
            if (StartsLikeCommand(message) == false) return false;

            foreach (Regex? commandRegex in commandsFactory.Keys)
            {
                commandTuple.match = commandRegex.Match(message);
                if (!commandTuple.match.Success) continue;

                if (!commandsCache.TryGetValue(commandRegex, out commandTuple.command))
                {
                    commandTuple.command = commandsFactory[commandRegex]();
                    commandsCache[commandRegex] = commandTuple.command;
                }

                return true;
            }

            return false;
        }

        public bool StartsLikeCommand(string message) =>
            message
               .AsSpan()
               .Trim()
               .StartsWith(CHAT_COMMAND_CHAR);
    }
}
