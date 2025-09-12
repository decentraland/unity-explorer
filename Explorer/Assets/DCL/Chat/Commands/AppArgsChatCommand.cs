using Cysharp.Threading.Tasks;
using Global.AppArgs;
using System.Text;
using System.Threading;

namespace DCL.Chat.Commands
{
    public class AppArgsCommand : IChatCommand
    {
        private readonly IAppArgs appArgs;

        public string Command => "app-args";

        public string Description => $"<b>/{Command}</b>\n  Shows the list of arguments the app launched with";

        public AppArgsCommand(IAppArgs appArgs)
        {
            this.appArgs = appArgs;
        }

        public UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("Application Arguments:");

            foreach ((string key, string value) in appArgs.Args())
            {
                sb.Append("Key: ").Append(key);
                if (string.IsNullOrWhiteSpace(value) == false) sb.Append(" Value: ").Append(value);
                sb.AppendLine();
            }

            return UniTask.FromResult(sb.ToString());
        }
    }
}
