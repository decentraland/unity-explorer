using Cysharp.Threading.Tasks;
using System;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using DCL.WebRequests;
using DCL.Utilities;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using SceneRunner.Admins;
using RichTypes;

namespace DCL.Chat.Commands
{
    public class SceneAdminsChatCommand : IChatCommand
    {
        private readonly IScenesCache sceneCache;

        public string Command => "scene-admins";

        public string Description => $"<b>/{Command}</b>\n  Shows the list of admins of the scene";

        public SceneAdminsChatCommand(IScenesCache sceneCache)
        {
            this.sceneCache = sceneCache;
        }

        public async UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            IReadonlyReactiveProperty<ISceneFacade?> currentSceneProp = sceneCache.CurrentScene;
            ISceneFacade? currentScene = currentSceneProp.Value;

            if (currentScene == null)
            {
                return "Current scene is not available. Please make sure you are on a scene";
            }

            Option<SceneAdmins> adminsOption = currentScene.SceneAdmins;

            if (adminsOption.Has == false)
            {
                return "Scene admins are not available in the current environment";
            }

            SceneAdmins admins = adminsOption.Value;

            Result<IReadOnlyDictionary<string, SceneAdmins.AdminInfo>> result = admins.CurrentAdmins();

            if (result.Success == false)
            {
                return result.ErrorMessage;
            }

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("Scene admins:");

            foreach (var item in result.Value)
            {
                SceneAdmins.AdminInfo i = item.Value;
                sb.Append("Id: ").AppendLine(i.id);
                sb.Append("Name: ").AppendLine(i.name);
                sb.Append("Admin: ").AppendLine(i.admin);
                sb.Append("Active: ").AppendLine(i.active);
                sb.Append("CanBeRemoved: ").Append(i.canBeRemoved).AppendLine();
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
