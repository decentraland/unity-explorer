using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.DebugUtilities.Views;
using System.Threading;

namespace DCL.Chat.Commands
{
    /// <summary>
    /// Toggles the debug panel, shows a list of available widgets or toggles a specific widget.
    ///
    /// Usage:
    ///     /debug
    ///     /debug help
    ///     /debug *widget*
    /// </summary>
    public class DebugPanelChatCommand : IChatCommand
    {
        public string Command => "debug";
        public string Description => "<b>/debug <i><widget | help></i></b>\n  Toggle debug panel or specific widget";
        public bool DebugOnly => true;

        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly IChatCommandsBus chatCommandsBus;

        public DebugPanelChatCommand(IDebugContainerBuilder debugContainerBuilder, IChatCommandsBus chatCommandsBus)//, ConnectionStatusPanelPlugin connectionStatusPanelPlugin)
        {
            this.debugContainerBuilder = debugContainerBuilder;
            this.chatCommandsBus = chatCommandsBus;
        }

        public bool ValidateParameters(string[] parameters) =>
            parameters.Length is 0 or 1;

        public UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            if (parameters.Length == 0)
            {
                bool visible = !debugContainerBuilder.IsVisible;
                debugContainerBuilder.IsVisible = visible;
                chatCommandsBus.SendConnectionStatusPanelChangedNotification(visible);
                return UniTask.FromResult(string.Empty);
            }

            string param = parameters[0];

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
