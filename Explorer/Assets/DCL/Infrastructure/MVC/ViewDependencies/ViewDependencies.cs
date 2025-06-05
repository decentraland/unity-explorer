using Cysharp.Threading.Tasks;
using DCL.Clipboard;
using DCL.Friends.UserBlocking;
using DCL.Input;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Profiles;
using DCL.UI;
using DCL.Utilities;
using System.Threading;
using UnityEngine;

namespace MVC
{
    /// <summary>
    ///     A set of references to the only systems and managers a view can use directly, without the need of a controller.
    ///     These should not be able to change the state of the game in a meaningful way, but allow for easier access to certain functionalities needed to display data.
    /// </summary>
    public class ViewDependencies
    {
        public readonly DCLInput DclInput;
        public readonly IEventSystem EventSystem;
        public readonly IMVCManagerMenusAccessFacade GlobalUIViews;
        public readonly IClipboardManager ClipboardManager;
        public readonly ICursor Cursor;
        public readonly ObjectProxy<IUserBlockingCache> UserBlockingCacheProxy;

        private readonly ISpriteCache thumbnailCache;
        private readonly IProfileRepository profileRepository;
        private readonly IRemoteMetadata remoteMetadata;

        public async UniTask<Sprite?> GetProfileThumbnailAsync(string thumbnailUrl, CancellationToken ct) =>
            await thumbnailCache.GetSpriteAsync(thumbnailUrl, ct);

        public Sprite? GetProfileThumbnail(string userId) =>
            thumbnailCache.GetCachedSprite(userId);

        public async UniTask<Profile?> GetProfileAsync(string walletId, CancellationToken ct) =>
            await profileRepository.GetAsync(walletId, 0, remoteMetadata.GetLambdaDomainOrNull(walletId), ct);

        public ViewDependencies(DCLInput dclInput, IEventSystem eventSystem, IMVCManagerMenusAccessFacade globalUIViews, IClipboardManager clipboardManager, ICursor cursor,
            ISpriteCache thumbnailCache, IProfileRepository profileRepository, IRemoteMetadata remoteMetadata, ObjectProxy<IUserBlockingCache> userBlockingCacheProxy)
        {
            DclInput = dclInput;
            EventSystem = eventSystem;
            GlobalUIViews = globalUIViews;
            ClipboardManager = clipboardManager;
            Cursor = cursor;
            this.thumbnailCache = thumbnailCache;
            this.profileRepository = profileRepository;
            this.remoteMetadata = remoteMetadata;
            this.UserBlockingCacheProxy = userBlockingCacheProxy;
        }

    }
}
