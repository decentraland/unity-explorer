using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using ECS.SceneLifeCycle.Realm;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Types;
using Random = UnityEngine.Random;
using static DCL.Chat.Commands.IChatCommand;

namespace Global.Dynamic.ChatCommands
{
    public class GoToChatCommand : IChatCommand
    {
        private const string COMMAND_GOTO_LOCAL = "goto-local";
        private const string PARAMETER_RANDOM = "random";

        public static readonly Regex REGEX = new ($@"^/({COMMAND_GOTO}|{COMMAND_GOTO_LOCAL})\s+(?:(-?\d+)\s*,\s*(-?\d+)|{PARAMETER_RANDOM})$", RegexOptions.Compiled);
        private readonly IRealmNavigator realmNavigator;

        private int x;
        private int y;

        public GoToChatCommand(IRealmNavigator realmNavigator)
        {
            this.realmNavigator = realmNavigator;
        }

        public async UniTask<string> ExecuteAsync(Match match, CancellationToken ct)
        {
            bool isLocal = match.Groups[1].Value == COMMAND_GOTO_LOCAL;

            if (match.Groups[2].Success && match.Groups[3].Success)
            {
                x = int.Parse(match.Groups[2].Value);
                y = int.Parse(match.Groups[3].Value);
            }
            else // means it's equal "random"
            {
                x = Random.Range(GenesisCityData.MIN_PARCEL.x, GenesisCityData.MAX_SQUARE_CITY_PARCEL.x);
                y = Random.Range(GenesisCityData.MIN_PARCEL.y, GenesisCityData.MAX_SQUARE_CITY_PARCEL.y);
            }

            var teleportResult =
                await realmNavigator.TryInitializeTeleportToParcelAsync(new Vector2Int(x, y), ct, isLocal);

            if (ct.IsCancellationRequested)
                return "🔴 Error. The operation was canceled!";


            return teleportResult.Success
                ? $"🟢 You teleported to {x},{y} in Genesis City"
                : "\ud83d\udd34 Teleport failed, please try again later!";
        }
    }
}
