using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.UI;
using System.Threading;
using UnityEngine;

namespace DCL.Chat.ChatCommands
{
    namespace DCL.Chat.ChatUseCases
    {
        public class GetCommunityThumbnailCommand
        {
            private readonly ISpriteCache spriteCache;
            private readonly ChatConfig.ChatConfig chatConfig;

            public GetCommunityThumbnailCommand(ISpriteCache spriteCache, ChatConfig.ChatConfig chatConfig)
            {
                this.spriteCache = spriteCache;
                this.chatConfig = chatConfig;
            }

            public async UniTask<Sprite> ExecuteAsync(string? thumbnailUrl, CancellationToken ct)
            {
                if (string.IsNullOrEmpty(thumbnailUrl))
                    return chatConfig.DefaultCommunityThumbnail;

                var sprite = await spriteCache.GetSpriteAsync(thumbnailUrl, true, ct);

                if (sprite != null)
                    return sprite;

                ReportHub.LogError(ReportCategory.COMMUNITIES,
                    $"Community thumbnail download failed for {thumbnailUrl}");

                return chatConfig.DefaultProfileThumbnail;
            }
        }
    }
}