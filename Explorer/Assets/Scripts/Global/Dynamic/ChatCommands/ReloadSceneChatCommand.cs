using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using ECS.SceneLifeCycle.Systems;
using System;

namespace Global.Dynamic.ChatCommands
{
    public class ReloadSceneChatCommand : IChatCommand
    {
        public static readonly Regex REGEX = new (@"^/reload(?:\s+(\w+))?$", RegexOptions.Compiled);

        private readonly ReloadSceneController reloadSceneController;

        public event Action? Executed;

        public ReloadSceneChatCommand(ReloadSceneController reloadSceneController)
        {
            this.reloadSceneController = reloadSceneController;
        }

        public async UniTask<string> ExecuteAsync(Match match, CancellationToken ct)
        {
            Executed?.Invoke();

            if (await reloadSceneController.TryReloadSceneAsync())
                return "🟢 Current scene has been reloaded";
            return "🔴 You need to be in a SDK7 scene to reload it.";
        }
    }
}
