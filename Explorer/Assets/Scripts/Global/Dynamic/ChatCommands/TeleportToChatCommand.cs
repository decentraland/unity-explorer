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
        private const string PARAMETER_RANDOM = "random";

        public static readonly Regex REGEX = new (
            pattern: "^/" + COMMAND_GOTO + @"\s+(?:(local)\s+)?(?:(-?\d+),(-?\d+)|" + PARAMETER_RANDOM + ")$",
            options: RegexOptions.Compiled);

        private readonly IRealmNavigator realmNavigator;

        private int x;
        private int y;
        private bool isLocal;

        public TeleportToChatCommand(IRealmNavigator realmNavigator)
        {
            this.realmNavigator = realmNavigator;
        }

        public async UniTask<string> ExecuteAsync(Match match, CancellationToken ct)
        {
            if (match.Groups[1].Success)
                isLocal = match.Groups[1].Value == "local";

            if (match.Groups[2].Success && match.Groups[3].Success)
            {
                x = int.Parse(match.Groups[2].Value);
                y = int.Parse(match.Groups[3].Value);
            }
            else if (match.Groups[1].Value == PARAMETER_RANDOM && !isLocal) // Random coordinates for Genesis City
            {
                x = Random.Range(GenesisCityData.MIN_PARCEL.x, GenesisCityData.MAX_SQUARE_CITY_PARCEL.x);
                y = Random.Range(GenesisCityData.MIN_PARCEL.y, GenesisCityData.MAX_SQUARE_CITY_PARCEL.y);
            }

            await realmNavigator.TeleportToParcelAsync(new Vector2Int(x, y), ct, isLocal);

            return ct.IsCancellationRequested
                ? "🔴 Error. The operation was canceled!"
                : $"🟢 You teleported to {x},{y} {(isLocal ? "locally" : "in Genesis City")}";
        }
    }
}
