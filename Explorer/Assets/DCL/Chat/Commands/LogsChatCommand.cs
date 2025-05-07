using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Chat.Commands
{
    public class LogsChatCommand : IChatCommand
    {
        public string Command => "logs";
        public string Description => "<b>/logs </b>\n Opens the logs folder";

        public UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            string path =
                Application.platform switch
                {
                    RuntimePlatform.WindowsPlayer => $@"file://{Environment.GetEnvironmentVariable("USERPROFILE")}\AppData\LocalLow\Decentraland\Explorer\",
                    RuntimePlatform.WindowsEditor => $@"file://{Environment.GetEnvironmentVariable("LOCALAPPDATA")}\Unity\Editor\",
                    RuntimePlatform.OSXPlayer => $"file://{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/Library/Logs/Decentraland/Explorer/",
                    RuntimePlatform.OSXEditor => $"file://{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/Library/Logs/Unity/",
                    _ => throw new NotSupportedException("Platform not supported."),
                };

            Application.OpenURL(path);

            return UniTask.FromResult(string.Empty);
        }
    }
}
