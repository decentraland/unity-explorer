using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using DCL.DebugUtilities;
using DCL.DebugUtilities.Views;
using System.Text.RegularExpressions;
using System.Threading;

namespace Global.Dynamic.ChatCommands
{
    public class DebugPanelChatCommand : IChatCommand
    {
        public static readonly Regex REGEX = new (@"^/debug(?:\s+(\w+))?$", RegexOptions.Compiled);

        private readonly IDebugContainerBuilder debugContainerBuilder;

        private string? param;

        public DebugPanelChatCommand(IDebugContainerBuilder debugContainerBuilder)
        {
            this.debugContainerBuilder = debugContainerBuilder;
        }

        public UniTask<string> ExecuteAsync(Match match, CancellationToken _)
        {
            param = match.Groups[1].Value;

            if (string.IsNullOrEmpty(param))
            {
                bool visible = !debugContainerBuilder.IsVisible;
                debugContainerBuilder.IsVisible = visible;

                return UniTask.FromResult(string.Empty);
            }

            if (param == "help")
            {
                string result = string.Empty;

                foreach (string? key in debugContainerBuilder.Widgets.Keys)
                    result += key + "\n";

                return UniTask.FromResult(result);
            }

            if (debugContainerBuilder.Widgets.TryGetValue(param!, out DebugWidget? widget))
            {
                widget.visible = !widget.visible;

                var hasOpenWidget = false;
                foreach (var otherWidget in debugContainerBuilder.Widgets.Values)
                    if (otherWidget.visible)
                    {
                        hasOpenWidget = true;
                        break;
                    }

                debugContainerBuilder.Container.visible = hasOpenWidget;

                return UniTask.FromResult(string.Empty);
            }

            return UniTask.FromResult($"🔴 Error. Widget '{param}' not found.");
        }
    }
}
