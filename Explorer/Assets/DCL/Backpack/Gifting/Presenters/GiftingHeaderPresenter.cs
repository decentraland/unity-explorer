using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Views;
using DCL.Profiles;

namespace DCL.Backpack.Gifting.Presenters
{
    public class GiftingHeaderPresenter
    {
        private readonly GiftingHeaderView view;
        private readonly IProfileRepository profileRepository;

        public GiftingHeaderPresenter(GiftingHeaderView view, IProfileRepository profileRepository, ISystemClipboard clipboard)
        {
            this.view = view;
            this.profileRepository = profileRepository;
        }

        public async UniTask SetupAsync(string userId, CancellationToken ct)
        {
            var profile = await profileRepository.GetAsync(userId, 0, ct: ct);
            if (profile == null) return;
        }
    }
}