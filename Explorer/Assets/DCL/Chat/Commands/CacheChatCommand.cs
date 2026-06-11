using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Cache.Disk;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Chat.Commands
{
    public class CacheChatCommand : IChatCommand
    {
        public string Command => "cache";
        public string Description => "<b>/cache </b>\n Opens the local disk cache folder";

        public UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            Application.OpenURL(new Uri(CacheDirectory.DefaultPath).AbsoluteUri);
            return UniTask.FromResult(string.Empty);
        }
    }
}
