using Cysharp.Threading.Tasks;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using DCL.WebRequests;

namespace DCL.Chat.Commands
{
    public struct Response
    {
        public string id;
        public string name;
        public string admin;
        public string active;
        public bool canBeRemoved;
    }

    public class SceneAdminsChatCommand : IChatCommand
    {
        private readonly IWebRequestController webRequestController;

        public string Command => "scene-admins";

        public string Description => $"<b>/{Command}</b>\n  Shows the list of admins of the scene";

        public SceneAdminsChatCommand(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        public async UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            // TODO replace to DecentralandUrlsSource
            const string url = "https://comms-gatekeeper.decentraland.zone/scene-admin";

            var list = await webRequestController.SignedFetchGetAsync(url, string.Empty, ct)
                .CreateFromJson<List<Response>>(WRJsonParser.Newtonsoft);
            
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("Scene admins:");

            foreach (Response r in list)
            {
                sb.Append("Id: ").Append(r.id);
                sb.Append("Name: ").Append(r.name);
                sb.Append("Admin: ").Append(r.admin);
                sb.Append("Active: ").Append(r.active);
                sb.Append("CanBeRemoved: ").Append(r.canBeRemoved);
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
