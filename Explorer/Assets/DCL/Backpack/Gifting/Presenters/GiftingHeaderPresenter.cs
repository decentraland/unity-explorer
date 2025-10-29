using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Views;
using DCL.Profiles;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;

namespace DCL.Backpack.Gifting.Presenters
{
    public class GiftingHeaderPresenter
    {
        private readonly GiftingHeaderView view;
        private readonly IProfileRepository profileRepository;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly UserWalletAddressElementController walletAddressController;

        private readonly ReactiveProperty<ProfileThumbnailViewModel> profilePictureViewModel;

        public GiftingHeaderPresenter(GiftingHeaderView view,
            IProfileRepository profileRepository,
            ProfileRepositoryWrapper  profileRepositoryWrapper )
        {
            this.view = view;
            this.profileRepository = profileRepository;
            this.profileRepositoryWrapper = profileRepositoryWrapper;

            walletAddressController = new UserWalletAddressElementController(view.UserProfileWallet);
            profilePictureViewModel = new ReactiveProperty<ProfileThumbnailViewModel>(new ProfileThumbnailViewModel());
        }

        public async UniTask SetupAsync(string userId, CancellationToken ct)
        {
            // Fetch the full profile of the person we are gifting to
            var profile = await profileRepository.GetAsync(userId, 0, ct: ct);
            if (profile == null) return;

            // Set the main title text
            view.Title.text = $"Send a Gift to {profile.Name}";

            // Use the existing ProfilePictureView's setup logic
            // Note: The 'Setup' method is obsolete, but we follow the existing pattern for now.
            // You will need a reference to a ProfileRepositoryWrapper if that's what your project uses.
            // Let's assume you can get one or adapt this call to the modern `Bind` method later.
            view.UserProfileImage.Setup(profileRepositoryWrapper, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl);

            // Delegate the wallet address logic to its dedicated controller
            walletAddressController.Setup(profile);
        }

        public void Dispose()
        {
            // Clean up the child controller
            walletAddressController.Dispose();
        }
    }
}