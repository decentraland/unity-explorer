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
        private readonly ProfileSectionElement element;
        private readonly ChatEntryConfigurationSO chatEntryConfiguration;

        public ProfileSectionController(
            ProfileSectionElement element,
            IWeb3IdentityCache identityCache,
            IProfileRepository profileRepository,
            IWebRequestController webRequestController,
            ChatEntryConfigurationSO chatEntryConfiguration)
        {
            this.identityCache = identityCache;
            this.profileRepository = profileRepository;
            this.element = element;
            this.chatEntryConfiguration = chatEntryConfiguration;

            nameElementController = new UserNameElementController(element.UserNameElement, chatEntryConfiguration);
            walletAddressElementController = new UserWalletAddressElementController(element.UserWalletAddressElement);
            profileImageController = new ImageController(element.FaceSnapshotImage, webRequestController);
        }

        public async UniTask LoadElements(CancellationToken ct)
        {
            await LoadAsync(ct);
        }

        private async UniTask LoadAsync(CancellationToken ct)
        {
            Profile? profile = await profileRepository.GetAsync(identityCache.Identity!.Address, 0, ct);

            nameElementController.Setup(profile);
            walletAddressElementController.Setup(profile);
            element.FaceFrame.color = chatEntryConfiguration.GetNameColor(profile?.Name);

            profileImageController!.StopLoading();
            //temporarily disabled the profile image request untill we have the correct
            //picture deployment
            //await profileImageController!.RequestImageAsync(profile.Avatar.FaceSnapshotUrl, ct);
        }
    }
}
