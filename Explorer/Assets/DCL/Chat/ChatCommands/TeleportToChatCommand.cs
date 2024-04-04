using Cysharp.Threading.Tasks;
using ECS.SceneLifeCycle.Realm;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

namespace DCL.Chat.ChatCommands
{
    internal class TeleportToChatCommand : IChatCommand
    {
        private readonly IRealmNavigator realmNavigator;

        private readonly int x;
        private readonly int y;

        public TeleportToChatCommand(Match match, IRealmNavigator realmNavigator)
        {
            this.realmNavigator = realmNavigator;

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
