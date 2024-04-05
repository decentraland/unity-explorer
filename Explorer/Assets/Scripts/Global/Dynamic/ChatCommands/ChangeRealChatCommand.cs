using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatCommands;
using ECS.SceneLifeCycle.Realm;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using static DCL.Chat.ChatCommands.IChatCommand;

namespace Global.Dynamic.ChatCommands
{
    public class ChangeRealmChatCommand : IChatCommand
    {
        private const string COMMAND_WORLD = "world";
        private const string PARAMETER_GENESIS = "genesis";

        public static readonly Regex REGEX = new ("^/(" + COMMAND_WORLD + "|" + COMMAND_GOTO + @")\s+(\S+\.dcl\.eth|" + PARAMETER_GENESIS + ")$", RegexOptions.Compiled);

        private readonly URLDomain worldDomain = URLDomain.FromString(IRealmNavigator.WORLDS_DOMAIN);

        private readonly Dictionary<string, URLAddress> worldAddressesCaches = new ();
        private readonly IRealmNavigator realmNavigator;

        private string? worldName;
        private string? realmUrl;

        public ChangeRealmChatCommand(IRealmNavigator realmNavigator)
        {
            this.realmNavigator = realmNavigator;
        }

        public void Set(Match match)
        {
            worldName = match.Groups[2].Value;
            realmUrl = worldName == PARAMETER_GENESIS ? IRealmNavigator.GENESIS_URL : GetWorldAddress(worldName);
            return;

            string GetWorldAddress(string worldPath)
            {
                if (!worldAddressesCaches.TryGetValue(worldPath, out URLAddress address))
                {
                    address = worldDomain.Append(URLPath.FromString(worldPath));
                    worldAddressesCaches.Add(worldPath, address);
                }

                return address.Value;
            }
        }

        public async UniTask<string> ExecuteAsync()
        {
            bool isSuccess = await realmNavigator.TryChangeRealmAsync(URLDomain.FromString(realmUrl!), CancellationToken.None);

            return isSuccess
                ? $"🟢 Welcome to the {worldName} world!"
                : $"🔴 Error. The world {worldName} doesn't exist!";
        }
    }
}
