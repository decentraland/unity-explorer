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
            // Resolves the same path the runtime writes to (and creates it if absent),
            // so there is no per-platform mapping to maintain here
            CacheDirectory cacheDirectory = CacheDirectory.NewDefault();

            // Uri escapes characters that would make a concatenated file URL invalid and be
            // silently ignored by OpenURL (e.g. the space in macOS' "Application Support")
            Application.OpenURL(new Uri(cacheDirectory.Path).AbsoluteUri);

            return UniTask.FromResult(string.Empty);
        }
    }
}
