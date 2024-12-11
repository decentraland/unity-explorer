using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using ECS.SceneLifeCycle;

namespace Global.Dynamic.ChatCommands
{
    public class ReloadSceneChatCommand : IChatCommand
    {
        public Regex Regex { get; } = new (@"^/reload(?:\s+(\w+))?$", RegexOptions.Compiled);
        public string Description => "/reload - Reload the current scene";

        private readonly ECSReloadScene reloadScene;

        public ReloadSceneChatCommand(ECSReloadScene reloadScene)
        {
            this.reloadScene = reloadScene;
        }

        public async UniTask<string> ExecuteAsync(Match match, CancellationToken ct)
        {
            if (await reloadScene.TryReloadSceneAsync(ct))
                return "🟢 Current scene has been reloaded";

            return "🔴 You need to be in a SDK7 scene to reload it.";
        }
    }
}
