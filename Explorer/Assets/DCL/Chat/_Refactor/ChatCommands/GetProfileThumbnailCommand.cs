using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.UI.Profiles.Helpers;
using UnityEngine;

using Utility;

namespace DCL.Chat.ChatUseCases
{
    public class GetProfileThumbnailCommand
    {
        private IEventBus eventBus;
        private readonly ChatConfig chatConfig;
        private readonly ProfileRepositoryWrapper profileRepository;

        public GetProfileThumbnailCommand(IEventBus eventBus,
            ChatConfig chatConfig,
            ProfileRepositoryWrapper profileRepository)
        {
            this.eventBus = eventBus;
            this.chatConfig = chatConfig;
            this.profileRepository = profileRepository;
        }

        public async UniTask<Sprite> ExecuteAsync(string userId, string faceSnapshotUrl, CancellationToken ct)
        {
            Sprite? cachedSprite = profileRepository.GetProfileThumbnail(userId);
            if (cachedSprite != null) return cachedSprite;

            try
            {
                var downloadedSprite = await profileRepository.GetProfileThumbnailAsync(faceSnapshotUrl, ct);
                return downloadedSprite ?? chatConfig.DefaultProfileThumbnail;
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.UI, $"Thumbnail download failed for {userId}: {e.Message}");
                return chatConfig.DefaultProfileThumbnail;
            }
        }
    }
}
