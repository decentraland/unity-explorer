using Cysharp.Threading.Tasks;
using System;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using DCL.WebRequests;
using DCL.Utilities;
using DCL.SceneBannedUsers;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using RichTypes;

namespace DCL.Chat.Commands
{
    public class SceneAdminsChatCommand : IChatCommand
    {
        public string Command => "scene-admins";

        public string Description => $"<b>/{Command}</b>\n  Shows the list of admins of the scene";

        public async UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            Result<IEnumerable<string>> result = RoomMetadataCurrentScene.Instance.CurrentAdmins();

            if (result.Success == false)
            {
                return result.ErrorMessage;
            }

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("Scene admins:");

            foreach (var item in result.Value)
            {
                sb.Append("Id: ").AppendLine(item);
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
