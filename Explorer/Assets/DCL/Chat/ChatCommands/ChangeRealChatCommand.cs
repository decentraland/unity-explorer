using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using ECS.SceneLifeCycle.Realm;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace DCL.Chat.ChatCommands
{
    internal class ChangeRealChatCommand : IChatCommand
    {
        private const string GENESIS_KEY = "genesis";
        private readonly URLDomain worldDomain = URLDomain.FromString(IRealmNavigator.WORLDS_DOMAIN);

        private readonly Dictionary<string, URLAddress> worldAddressesCaches = new ();
        private readonly IRealmNavigator realmNavigator;

        private readonly string worldName;
        private readonly string realmUrl;

        public ChangeRealChatCommand(Match match, IRealmNavigator realmNavigator)
        {
            this.realmNavigator = realmNavigator;

            worldName = match.Groups[2].Value;
            realmUrl = worldName == GENESIS_KEY ? IRealmNavigator.GENESIS_URL : GetWorldAddress(worldName);
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

        public async UniTask<string> ExecuteAsync()
        {
            bool isSuccess = await realmNavigator.TryChangeRealmAsync(realmUrl, CancellationToken.None);

            return isSuccess
                ? $"🟢 Welcome to the {worldName} world!"
                : $"🔴 Error. The world {worldName} doesn't exist!";
        }
    }
}
