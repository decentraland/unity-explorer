using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Web3;

namespace DCL.Backpack.AvatarSection.Outfits.Commands
{
    public class CheckOutfitsBannerVisibilityCommand
    {
        private readonly ISelfProfile selfProfile;
        private readonly INftNamesProvider nftNamesProvider;

        public CheckOutfitsBannerVisibilityCommand(ISelfProfile selfProfile,
            INftNamesProvider nftNamesProvider)
        {
            this.selfProfile = selfProfile;
            this.nftNamesProvider = nftNamesProvider;
        }

        public async UniTask<bool> ShouldShowExtraOutfitSlotsAsync(CancellationToken ct)
        {
            if (!(await selfProfile.ProfileAsync(ct)).IsOk(out Profile? profile))
                return false;

            INftNamesProvider.PaginatedNamesResponse names = await nftNamesProvider.GetAsync(new Web3Address(profile.UserId), 1, 1, ct);
            return names.TotalAmount > 0;
        }
    }
}