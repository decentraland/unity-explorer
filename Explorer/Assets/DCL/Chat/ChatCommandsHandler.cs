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
        private const string GENESIS_KEY = "genesis";

        private static readonly Regex CHANGE_REALM_REGEX = new (@"^/(world|goto)\s+(\S+\.dcl\.eth|" + GENESIS_KEY + ")$", RegexOptions.Compiled);
        private static readonly Regex TELEPORT_REGEX = new (@"^/goto\s+(-?\d+),(-?\d+)$", RegexOptions.Compiled);

        private readonly IRealmNavigator realmNavigator;

        private readonly URLDomain worldDomain = URLDomain.FromString(IRealmNavigator.WORLDS_DOMAIN);

        private readonly Dictionary<string, URLAddress> worldAddressesCaches = new ();

        public ChatCommandsHandler(IRealmNavigator realmNavigator)
        {
            this.realmNavigator = realmNavigator;
        }

        public bool TryGetChatCommand(string message, ref UniTask<string> command)
        {
            if (!message.StartsWith(CHAT_COMMAND_CHAR)) return false;

            if (CHANGE_REALM_REGEX.IsMatch(message))
            {
                command = ChangeRealmCommand(message);
                return true;
            }

            if (TELEPORT_REGEX.IsMatch(message))
            {
                command = TeleportToCommand(message);
                return true;
            }

            return false;
        }

        private async UniTask<string> TeleportToCommand(string message)
        {
            Match match = TELEPORT_REGEX.Match(message);
            var x = int.Parse(match.Groups[1].Value);
            var y = int.Parse(match.Groups[2].Value);

            await realmNavigator.TeleportToParcelAsync(new Vector2Int(x, y), CancellationToken.None);
            return $"🟢 You teleported to {x},{y} in Genesis City";
        }

        private async UniTask<string> ChangeRealmCommand(string message)
        {
            Match match = CHANGE_REALM_REGEX.Match(message);
            string worldName = match.Groups[2].Value;
            string realmUrl = worldName == GENESIS_KEY ? "https://peer.decentraland.org" : GetWorldAddress(worldName);

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
