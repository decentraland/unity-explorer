using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using ECS.SceneLifeCycle.Realm;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

namespace DCL.Chat
{
    internal class ChatCommandsHandler
    {
        private const char CHAT_COMMAND_CHAR = '/';

        private const string GOTO_KEY = "goto";
        private const string WORLD_KEY = "world";
        private const string GENESIS_KEY = "genesis";
        private const string RANDOM_KEY = "random";

        private static readonly Regex CHANGE_REALM_REGEX = new ("^/(" + WORLD_KEY + "|" + GOTO_KEY + @")\s+(\S+\.dcl\.eth|" + GENESIS_KEY + ")$", RegexOptions.Compiled);
        private static readonly Regex TELEPORT_REGEX = new ("^/" + GOTO_KEY + @"\s+((?:-?\d+),(-?\d+)|" + RANDOM_KEY + ")$", RegexOptions.Compiled);

        private readonly IRealmNavigator realmNavigator;

        private readonly URLDomain worldDomain = URLDomain.FromString(IRealmNavigator.WORLDS_DOMAIN);

        private readonly Dictionary<string, URLAddress> worldAddressesCaches = new ();

        public ChatCommandsHandler(IRealmNavigator realmNavigator)
        {
            this.realmNavigator = realmNavigator;
        }

        public bool TryGetChatCommand(in string message, ref UniTask<string> command)
        {
            if (!message.StartsWith(CHAT_COMMAND_CHAR)) return false;

            if (CHANGE_REALM_REGEX.IsMatch(message))
            {
                command = ChangeRealmCommandAsync(message);
                return true;
            }

            if (TELEPORT_REGEX.IsMatch(message))
            {
                command = TeleportToCommandAsync(message);
                return true;
            }

            return false;
        }

        private async UniTask<string> TeleportToCommandAsync(string message)
        {
            Match match = TELEPORT_REGEX.Match(message);

            bool isRandom = match.Groups[1].Value == RANDOM_KEY;

            int x = isRandom ? Random.Range(-150, 150) : int.Parse(match.Groups[1].Value);
            int y = isRandom ? Random.Range(-150, 150) : int.Parse(match.Groups[2].Value);

            await realmNavigator.TeleportToParcelAsync(new Vector2Int(x, y), CancellationToken.None);
            return $"🟢 You teleported to {x},{y} in Genesis City";
        }

        private async UniTask<string> ChangeRealmCommandAsync(string message)
        {
            Match match = CHANGE_REALM_REGEX.Match(message);
            string worldName = match.Groups[2].Value;
            string realmUrl = worldName == GENESIS_KEY ? IRealmNavigator.GENESIS_URL : GetWorldAddress(worldName);

            bool isSuccess = await realmNavigator.TryChangeRealmAsync(realmUrl, CancellationToken.None);

            return isSuccess
                ? $"🟢 Welcome to the {worldName} world!"
                : $"🔴 Error. The world {worldName} doesn't exist!";
        }

        private string GetWorldAddress(string worldPath)
        {
            if (!worldAddressesCaches.TryGetValue(worldPath, out URLAddress address))
            {
                address = worldDomain.Append(URLPath.FromString(worldPath));
                worldAddressesCaches.Add(worldPath, address);
            }

            return address.Value;
        }
    }
}
