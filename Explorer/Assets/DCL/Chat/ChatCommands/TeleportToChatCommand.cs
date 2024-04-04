using Cysharp.Threading.Tasks;
using ECS.SceneLifeCycle.Realm;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using Random = UnityEngine.Random;
using static DCL.Chat.ChatCommands.IChatCommand;

namespace DCL.Chat.ChatCommands
{
    internal class TeleportToChatCommand : IChatCommand
    {
        private const string PARAMETER_RANDOM = "random";

        internal static readonly Regex REGEX = new ("^/" + COMMAND_GOTO + @"\s+(?:(-?\d+),(-?\d+)|" + PARAMETER_RANDOM + ")$", RegexOptions.Compiled);

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
            else
            {
                x = Random.Range(-150, 150);
                y = Random.Range(-150, 150);
            }
        }

        public async UniTask<string> ExecuteAsync()
        {
            await realmNavigator.TeleportToParcelAsync(new Vector2Int(x, y), CancellationToken.None);
            return $"🟢 You teleported to {x},{y} in Genesis City";
        }
    }
}
