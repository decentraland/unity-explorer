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

        public static readonly Regex REGEX = new ("^/" + COMMAND_GOTO + @"\s+(?:(-?\d+),(-?\d+)|" + PARAMETER_RANDOM + ")$", RegexOptions.Compiled);

        private readonly IRealmNavigator realmNavigator;

        private int x;
        private int y;

        public TeleportToChatCommand(IRealmNavigator realmNavigator)
        {
            this.realmNavigator = realmNavigator;
        }

        public void Set(Match match)
        {
            if (match.Groups[1].Success && match.Groups[2].Success)
            {
                x = int.Parse(match.Groups[1].Value);
                y = int.Parse(match.Groups[2].Value);
            }
            else // means that it was PARAMETER_RANDOM
            {
                x = Random.Range(GenesisCityData.MIN_PARCEL.x, GenesisCityData.MAX_SQUARE_CITY_PARCEL.x);
                y = Random.Range(GenesisCityData.MIN_PARCEL.y, GenesisCityData.MAX_SQUARE_CITY_PARCEL.y);
            }
        }

        public async UniTask<string> ExecuteAsync(CancellationToken ct)
        {
            await realmNavigator.TeleportToParcelAsync(new Vector2Int(x, y), ct);

            return ct.IsCancellationRequested
                ? "🔴 Error. The operation was canceled!"
                : $"🟢 You teleported to {x},{y} in Genesis City";
        }
    }
}
