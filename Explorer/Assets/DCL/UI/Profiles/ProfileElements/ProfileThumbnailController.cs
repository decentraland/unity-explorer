using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Web3.Identities;
using DCL.WebRequests;
using MVC;
using System.Threading;
using Utility;

namespace DCL.UI.ProfileElements
{
    public class ProfileThumbnailController : ControllerBase<ProfileThumbnailView>
    {
        private readonly IWeb3IdentityCache identityCache;
        private readonly IProfileRepository profileRepository;
        private readonly IWebRequestController webRequestController;

        private ImageController profileImageController;
        private CancellationTokenSource cts;

        public ProfileThumbnailController(
            ViewFactoryMethod viewFactory,
            IWeb3IdentityCache identityCache,
            IProfileRepository profileRepository,
            IWebRequestController webRequestController
        ) : base(viewFactory)
        {
            this.identityCache = identityCache;
            this.profileRepository = profileRepository;
            this.webRequestController = webRequestController;
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();
            cts = cts.SafeRestart();
            SetupAsync(cts.Token).Forget();
        }

        private async UniTaskVoid SetupAsync(CancellationToken ct)
        {
            Profile? profile = await profileRepository.GetAsync(identityCache.Identity!.Address, ct);

            if (profile == null) return;

            profileImageController!.StopLoading();

            //temporarily disabled the profile image request until we have the correct
            //picture deployment
            //await profileImageController!.RequestImageAsync(profile.Avatar.FaceSnapshotUrl, ct);
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);

        public new void Dispose()
        {
            cts.SafeCancelAndDispose();
            profileImageController.StopLoading();
        }
    }
}
