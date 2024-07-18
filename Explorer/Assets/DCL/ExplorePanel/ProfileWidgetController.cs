using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.UI;
using DCL.Web3.Identities;
using DCL.WebRequests;
using MVC;
using System.Threading;
using Utility;

namespace DCL.ExplorePanel
{
    public class ProfileWidgetController : ControllerBase<ProfileWidgetView>
    {
        private readonly IWeb3IdentityCache identityCache;
        private readonly IProfileRepository profileRepository;
        private readonly IWebRequestController webRequestController;

        private ImageController? profileImageController;
        private CancellationTokenSource? loadProfileCts;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public ProfileWidgetController(ViewFactoryMethod viewFactory,
            IWeb3IdentityCache identityCache,
            IProfileRepository profileRepository,
            IWebRequestController webRequestController)
            : base(viewFactory)
        {
            this.identityCache = identityCache;
            this.profileRepository = profileRepository;
            this.webRequestController = webRequestController;
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            profileImageController = new ImageController(viewInstance.FaceSnapshotImage, webRequestController);
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
            Profile? profile = await profileRepository.GetAsync(identityCache.Identity!.Address, 0, ct);

            if (viewInstance.NameLabel != null) viewInstance.NameLabel.text = profile?.Name ?? "Guest";

            if (viewInstance.AddressLabel != null)
            {
                viewInstance.AddressLabel.gameObject.SetActive(!profile!.HasClaimedName);

                if (!profile.HasClaimedName)
                    viewInstance.AddressLabel.text = $"#{profile.UserId[^4..]}";
            }

            profileImageController!.StopLoading();

            //temporarily disabled the profile image request untill we have the correct
            //picture deployment
            //await profileImageController!.RequestImageAsync(profile.Avatar.FaceSnapshotUrl, ct);
        }
    }
}
