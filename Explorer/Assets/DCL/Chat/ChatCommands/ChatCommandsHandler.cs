using ECS.SceneLifeCycle.Realm;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DCL.Chat.ChatCommands
{
    internal class ChatCommandsHandler
    {
        private const char CHAT_COMMAND_CHAR = '/';

        private const string GOTO_KEY = "goto";
        private const string WORLD_KEY = "world";
        private const string RANDOM_KEY = "random";
        private const string GENESIS_KEY = "genesis";

        private static readonly Regex CHANGE_REALM_REGEX = new ("^/(" + WORLD_KEY + "|" + GOTO_KEY + @")\s+(\S+\.dcl\.eth|" + GENESIS_KEY + ")$", RegexOptions.Compiled);
        private static readonly Regex TELEPORT_REGEX = new ("^/" + GOTO_KEY + @"\s+(?:(-?\d+),(-?\d+)|" + RANDOM_KEY + ")$", RegexOptions.Compiled);
        private readonly Dictionary<Regex, Func<IChatCommand>> commandsFactory;
        private readonly Dictionary<Regex, IChatCommand> commandsCache = new ();

        private readonly List<Regex> commandsRegex = new ()
        {
            CHANGE_REALM_REGEX,
            TELEPORT_REGEX,
        };

        public ChatCommandsHandler(IRealmNavigator realmNavigator)
        {
            commandsFactory = new Dictionary<Regex, Func<IChatCommand>>
            {
                { CHANGE_REALM_REGEX, () => new ChangeRealmChatCommand(realmNavigator) },
                { TELEPORT_REGEX, () => new TeleportToChatCommand(realmNavigator) },
            };
        }

        public bool TryGetChatCommand(in string message, ref IChatCommand command)
        {
            if (!message.StartsWith(CHAT_COMMAND_CHAR)) return false;

            foreach (Regex? commandRegex in commandsRegex)
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
