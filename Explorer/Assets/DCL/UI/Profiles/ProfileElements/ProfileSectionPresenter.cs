using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.Web3.Identities;
using System;
using System.Threading;
using Utility;

namespace DCL.UI.ProfileElements
{
    public class ProfileSectionPresenter: IDisposable
    {
        private readonly ProfileSectionView view;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IProfileRepository profileRepository;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly UserNameElementPresenter userNameElementPresenter;
        private readonly UserWalletAddressElementPresenter walletAddressElementPresenter;
        private CancellationTokenSource cts = new ();

        public ProfileSectionPresenter(
            ProfileSectionView view,
            IWeb3IdentityCache identityCache,
            IProfileRepository profileRepository,
            ProfileRepositoryWrapper profileDataProvider)
        {
            this.view = view;
            this.identityCache = identityCache;
            this.profileRepository = profileRepository;
            this.profileRepositoryWrapper = profileDataProvider;

            userNameElementPresenter = new UserNameElementPresenter(view.UserNameElement);
            walletAddressElementPresenter = new UserWalletAddressElementPresenter(view.UserWalletAddressElement);
        }

        public void Dispose()
        {
            cts.SafeCancelAndDispose();
            userNameElementPresenter.Dispose();
            walletAddressElementPresenter.Dispose();
        }

        public void SetupProfile(CancellationToken ct)
        {
            cts = cts.SafeRestartLinked(ct);
            SetupProfileAsync().Forget();
            return;

            async UniTaskVoid SetupProfileAsync()
            {
                if (identityCache.Identity == null)
                {
                    ReportHub.LogError(ReportCategory.PROFILE, "Cannot setup own profile. Identity is null.");
                    return;
                }

                Profile? profile = await profileRepository.GetAsync(identityCache.Identity!.Address, cts.Token);

                if (profile == null) return;

                userNameElementPresenter.Setup(profile);
                walletAddressElementPresenter.Setup(profile);
                view.ProfilePictureView.Setup(profileRepositoryWrapper, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl, profile.UserId);
            }
        }
    }
}
