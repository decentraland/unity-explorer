using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Profiles;
using DCL.UI;
using DCL.UI.ProfileElements;
using DCL.Web3.Identities;
using DCL.WebRequests;
using System.Threading;
using Utility;

namespace DCL.ExplorePanel
{
    public class ProfileSectionController
    {
        private readonly IWeb3IdentityCache identityCache;
        private readonly IProfileRepository profileRepository;
        private readonly ImageController profileImageController;
        private readonly UserNameElementController nameElementController;
        private readonly UserWalletAddressElementController walletAddressElementController;

        private CancellationTokenSource? loadProfileCts;

        public ProfileSectionController(
            ProfileSectionElement element,
            IWeb3IdentityCache identityCache,
            IProfileRepository profileRepository,
            IWebRequestController webRequestController,
            ChatEntryConfigurationSO chatEntryConfiguration)
        {
            this.identityCache = identityCache;
            this.profileRepository = profileRepository;

            nameElementController = new UserNameElementController(element.UserNameElement, chatEntryConfiguration);
            walletAddressElementController = new UserWalletAddressElementController(element.UserWalletAddressElement);
            profileImageController = new ImageController(element.FaceSnapshotImage, webRequestController);
        }

        public void LoadElements()
        {
            loadProfileCts = loadProfileCts.SafeRestart();
            LoadAsync(loadProfileCts.Token).Forget();
        }

        private async UniTaskVoid LoadAsync(CancellationToken ct)
        {
            Profile? profile = await profileRepository.GetAsync(identityCache.Identity!.Address, 0, ct);

            nameElementController.Setup(profile);
            walletAddressElementController.Setup(profile);

            profileImageController!.StopLoading();
            //temporarily disabled the profile image request untill we have the correct
            //picture deployment
            //await profileImageController!.RequestImageAsync(profile.Avatar.FaceSnapshotUrl, ct);
        }
    }
}
