using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Chat;
using ECS.SceneLifeCycle.Realm;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using static DCL.Chat.IChatCommand;

namespace Global.Dynamic.ChatCommands
{
    public class ChangeRealmChatCommand : IChatCommand
    {
        private const string COMMAND_WORLD = "world";
        private const string WORLD_SUFFIX = ".dcl.eth";

        // Parameters to URL mapping
        private static readonly Dictionary<string, string> PARAMETER_URLS = new()
        {
            {"genesis", IRealmNavigator.GENESIS_URL},
            {"goerli", IRealmNavigator.GOERLI_URL},
            {"goerli_old", IRealmNavigator.GOERLI_OLD_URL},
            {"stream", IRealmNavigator.STREAM_WORLD_URL},
            {"sdk", IRealmNavigator.SDK_TEST_SCENES_URL},
            {"test", IRealmNavigator.TEST_SCENES_URL},
        };

        public static readonly Regex REGEX = new ($@"^/({COMMAND_WORLD}|{COMMAND_GOTO})\s+((?!-?\d+,-?\d+$).+)$", RegexOptions.Compiled);

        private readonly URLDomain worldDomain = URLDomain.FromString(IRealmNavigator.WORLDS_DOMAIN);

        private readonly Dictionary<string, URLAddress> worldAddressesCaches = new ();
        private readonly IRealmNavigator realmNavigator;

        private string? worldName;
        private string? realmUrl;

        public ChangeRealmChatCommand(IRealmNavigator realmNavigator)
        {
            this.realmNavigator = realmNavigator;
        }

        public async UniTask<string> ExecuteAsync(Match match, CancellationToken ct)
        {
            worldName = match.Groups[2].Value;

            if (!PARAMETER_URLS.TryGetValue(worldName, out realmUrl))
            {
                if (!worldName.EndsWith(WORLD_SUFFIX))
                    worldName += WORLD_SUFFIX;

                realmUrl = GetWorldAddress(worldName);
            }

            bool isSuccess = await realmNavigator.TryChangeRealmAsync(URLDomain.FromString(realmUrl!), ct);

            if (ct.IsCancellationRequested)
                return "🔴 Error. The operation was canceled!";

            return isSuccess
                ? $"🟢 Welcome to the {worldName} world!"
                : $"🔴 Error. The world {worldName} doesn't exist or not reachable!";
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
