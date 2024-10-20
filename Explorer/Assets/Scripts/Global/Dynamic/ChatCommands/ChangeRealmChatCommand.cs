﻿using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using DCL.Multiplayer.Connections.DecentralandUrls;
using ECS.SceneLifeCycle.Realm;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

namespace Global.Dynamic.ChatCommands
{
    /// <summary>
    /// <example>
    /// Commands could be:
    ///     "/world genesis"
    ///     "/goto goerli"
    ///     "/world goerli 77,1"
    /// </example>
    /// </summary>
    public class ChangeRealmChatCommand : IChatCommand
    {
        private const string COMMAND_WORLD = "world";
        private const string WORLD_SUFFIX = ".dcl.eth";

        // Parameters to URL mapping
        private readonly Dictionary<string, string> paramUrls;

        public static readonly Regex REGEX =
            new(
                $@"^/({COMMAND_WORLD}|{ChatCommandsUtils.COMMAND_GOTO})\s+((?!-?\d+\s*,\s*-?\d+$).+?)(?:\s+(-?\d+)\s*,\s*(-?\d+))?$",
                RegexOptions.Compiled);
        private readonly URLDomain worldDomain = URLDomain.FromString(IRealmNavigator.WORLDS_DOMAIN);

        private readonly Dictionary<string, URLAddress> worldAddressesCaches = new ();
        private readonly IRealmNavigator realmNavigator;

        private string? worldName;
        private string? realmUrl;

        public ChangeRealmChatCommand(IRealmNavigator realmNavigator, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.realmNavigator = realmNavigator;

            paramUrls = new Dictionary<string, string>
            {
                { "genesis", decentralandUrlsSource.Url(DecentralandUrl.Genesis) },
                { "goerli", IRealmNavigator.GOERLI_URL },
                { "goerli-old", IRealmNavigator.GOERLI_OLD_URL },
                { "stream", IRealmNavigator.STREAM_WORLD_URL },
                { "sdk", IRealmNavigator.SDK_TEST_SCENES_URL },
                { "test", IRealmNavigator.TEST_SCENES_URL },
            };
        }

        public async UniTask<string> ExecuteAsync(Match match, CancellationToken ct)
        {
            worldName = match.Groups[2].Value;

            if (worldName.StartsWith("https"))
            {
                realmUrl = worldName;
            }
            else if (!paramUrls.TryGetValue(worldName, out realmUrl))
            {
                if (!worldName.EndsWith(WORLD_SUFFIX))
                    worldName += WORLD_SUFFIX;

                realmUrl = GetWorldAddress(worldName);
            }

            var realm = URLDomain.FromString(realmUrl!);

            Vector2Int parcel = default;

            if (match.Groups[3].Success && match.Groups[4].Success)
                parcel = new Vector2Int(int.Parse(match.Groups[3].Value), int.Parse(match.Groups[4].Value));

            if (!realmNavigator.CheckIsNewRealm(realm))
                return $"🟡 You are already in {worldName}!";

            if (!await realmNavigator.CheckRealmIsReacheableAsync(realm, ct))
                return $"🔴 Error. The world {worldName} doesn't exist or not reachable!";

            var result = await realmNavigator.TryChangeRealmAsync(realm, ct, parcel);

            if (ct.IsCancellationRequested)
                return "🔴 Error. The operation was canceled!";

            return result.Success
                ? $"🟢 Welcome to the {worldName} world!"
                : $"🔴 Teleport was not fully successful to {worldName} world"!;
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
