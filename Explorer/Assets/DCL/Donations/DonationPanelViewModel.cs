using DCL.Profiles;
using DCL.UI.ProfileElements;
using DCL.Utilities;

namespace DCL.Donations
{
    public class DonationPanelViewModel
    {
        public IReactiveProperty<ProfileThumbnailViewModel> ProfileThumbnail { get; }
            = new ReactiveProperty<ProfileThumbnailViewModel>(ProfileThumbnailViewModel.Default());

        public Profile.CompactInfo? Profile { get; }
        public string SceneCreatorAddress { get; }
        public string SceneName { get; }
        public decimal CurrentBalance { get; }
        public decimal[] SuggestedDonationAmount { get; }
        public decimal ManaUsdPrice { get; }

        public DonationPanelViewModel(Profile.CompactInfo? profile,
            string sceneCreatorAddress,
            string sceneName,
            decimal currentBalance,
            decimal[] suggestedDonationAmount,
            decimal manaUsdPrice)
        {
            Profile = profile;
            SceneCreatorAddress = sceneCreatorAddress;
            SceneName = sceneName;
            CurrentBalance = currentBalance;
            SuggestedDonationAmount = suggestedDonationAmount;
            ManaUsdPrice = manaUsdPrice;
        }
    }
}
