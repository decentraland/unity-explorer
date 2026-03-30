using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Chat.Commands
{
    public class FpsChatCommand : IChatCommand
    {
        public string Command => "fps";
        public string Description => "<b>/fps (show|unset)</b>\n show target fps and v-sync or unset the values";

        public bool ValidateParameters(string[] parameters) =>
            parameters.Length == 1;

        public UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            if (parameters.Length < 1)
            {
                return UniTask.FromResult("Provide show or unset parameter");
            }

            string request = parameters[0].ToLower();

            string msg = string.Empty;

            switch (request)
            {
                case "show":
                    msg = $"v-sync count: {QualitySettings.vSyncCount}; target frame rate: {Application.targetFrameRate}";
                    break;
                case "unset":
                    QualitySettings.vSyncCount = 0;
                    Application.targetFrameRate = -1;
                    msg =  "successful unset";
                    break;
                default:
                    msg = $"unknown: {request}";
                    break;
            }

            return UniTask.FromResult(msg);
        }
    }
}
