using Cysharp.Threading.Tasks;
using DCL.Chat;
using ECS.SceneLifeCycle.Realm;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using Utility;
using Random = UnityEngine.Random;
using static DCL.Chat.IChatCommand;

namespace Global.Dynamic.ChatCommands
{
    public class TeleportToChatCommand : IChatCommand
    {
        private const string COMMAND_GOTO_LOCAL = "goto-local";
        private const string PARAMETER_RANDOM = "random";

        public static readonly Regex REGEX = new ($@"^/({COMMAND_GOTO}|{COMMAND_GOTO_LOCAL})\s+(?:(-?\d+),(-?\d+)|{PARAMETER_RANDOM})$",RegexOptions.Compiled);

        private readonly IRealmNavigator realmNavigator;

        private int x;
        private int y;

        public TeleportToChatCommand(IRealmNavigator realmNavigator)
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

            await realmNavigator.TryInitializeTeleportToParcelAsync(new Vector2Int(x, y), ct, isLocal);

            return ct.IsCancellationRequested
                ? "🔴 Error. The operation was canceled!"
                : $"🟢 You teleported to {x},{y} in Genesis City";
        }
    }
}
