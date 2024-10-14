using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.UI;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.ArgsFactory;
using MVC;
using System.Threading;
using Utility;

namespace DCL.UI.ProfileElements
{
    public class ProfileWidgetController : ControllerBase<ProfileWidgetView>
    {
        private const string GUEST_NAME = "Guest";

        private readonly IWeb3IdentityCache identityCache;
        private readonly IProfileRepository profileRepository;
        private readonly IWebRequestController webRequestController;
        private readonly IGetTextureArgsFactory getTextureArgsFactory;

        private ImageController? profileImageController;
        private CancellationTokenSource? loadProfileCts;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public ProfileWidgetController(ViewFactoryMethod viewFactory,
            IWeb3IdentityCache identityCache,
            IProfileRepository profileRepository,
            IWebRequestController webRequestController,
            IGetTextureArgsFactory getTextureArgsFactory
        ) : base(viewFactory)
        {
            this.identityCache = identityCache;
            this.profileRepository = profileRepository;
            this.webRequestController = webRequestController;
            this.getTextureArgsFactory = getTextureArgsFactory;
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            profileImageController = new ImageController(viewInstance.FaceSnapshotImage, webRequestController, getTextureArgsFactory);
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();

            loadProfileCts = loadProfileCts.SafeRestart();
            LoadAsync(loadProfileCts.Token).Forget();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);

        private async UniTaskVoid LoadAsync(CancellationToken ct)
        {
            Profile? profile = await profileRepository.GetAsync(identityCache.Identity!.Address, ct);

            if (viewInstance.NameLabel != null) viewInstance.NameLabel.text = profile?.Name ?? GUEST_NAME;

            if (viewInstance.AddressLabel != null)
            {
                if (profile is { HasClaimedName: false })
                    viewInstance.AddressLabel.text = $"#{profile.UserId[^4..]}";
            }

            profileImageController!.StopLoading();

            //temporarily disabled the profile image request untill we have the correct
            //picture deployment
            //await profileImageController!.RequestImageAsync(profile.Avatar.FaceSnapshotUrl, ct);
        }
    }
}
