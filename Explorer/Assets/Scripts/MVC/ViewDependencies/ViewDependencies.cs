using Cysharp.Threading.Tasks;
using DCL.Clipboard;
using DCL.Input;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Profiles;
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

        private readonly IProfileThumbnailCache thumbnailCache;
        private readonly RealmProfileRepository profileRepository;
        private readonly IRemoteMetadata remoteMetadata;

        public async UniTask<Sprite> GetThumbnailAsync(string userId, string thumbnailUrl, CancellationToken ct) =>
            await thumbnailCache.GetThumbnailAsync(userId, thumbnailUrl, ct);

        public async UniTask<Profile> GetProfileAsync(string walletId, CancellationToken ct) =>
            await profileRepository.GetAsync(walletId, 0, remoteMetadata.GetLambdaDomainOrNull(walletId), ct);

        public ViewDependencies(DCLInput dclInput, IEventSystem eventSystem, IMVCManagerMenusAccessFacade globalUIViews, IClipboardManager clipboardManager, ICursor cursor,
            IProfileThumbnailCache thumbnailCache, RealmProfileRepository profileRepository, IRemoteMetadata remoteMetadata)
        {
            DclInput = dclInput;
            EventSystem = eventSystem;
            GlobalUIViews = globalUIViews;
            ClipboardManager = clipboardManager;
            Cursor = cursor;
            this.thumbnailCache = thumbnailCache;
            this.profileRepository = profileRepository;
            this.remoteMetadata = remoteMetadata;
        }

    }
}
