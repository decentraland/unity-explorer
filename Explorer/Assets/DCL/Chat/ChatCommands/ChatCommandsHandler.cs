using ECS.SceneLifeCycle.Realm;
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

        private readonly IRealmNavigator realmNavigator;

        public ChatCommandsHandler(IRealmNavigator realmNavigator)
        {
            this.realmNavigator = realmNavigator;
        }

        public bool TryGetChatCommand(in string message, ref IChatCommand command)
        {
            if (!message.StartsWith(CHAT_COMMAND_CHAR)) return false;

            Match match = CHANGE_REALM_REGEX.Match(message);
            if (match.Success)
            {
                command = new ChangeRealChatCommand(match, realmNavigator);
                return true;
            }

            match = TELEPORT_REGEX.Match(message);
            if (match.Success)
            {
                command = new TeleportToChatCommand(match, realmNavigator);
                return true;
            }

            return false;
        }
    }
}
