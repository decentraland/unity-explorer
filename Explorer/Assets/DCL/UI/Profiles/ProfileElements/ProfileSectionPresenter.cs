using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
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
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly UserNameElementPresenter userNameElementPresenter;
        private readonly UserWalletAddressElementPresenter walletAddressElementPresenter;
        private readonly IReactiveProperty<ProfileThumbnailViewModel> profileThumbnail = new ReactiveProperty<ProfileThumbnailViewModel>(ProfileThumbnailViewModel.Default());

        private CancellationTokenSource cts = new ();

        public ProfileSectionPresenter(
            ProfileSectionView view,
            IWeb3IdentityCache identityCache,
            ProfileRepositoryWrapper profileDataProvider)
        {
            this.view = view;
            this.identityCache = identityCache;
            this.profileRepositoryWrapper = profileDataProvider;

            userNameElementPresenter = new UserNameElementPresenter(view.UserNameElement);
            walletAddressElementPresenter = new UserWalletAddressElementPresenter(view.UserWalletAddressElement);
            view.ProfilePictureView.Bind(profileThumbnail);
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

                var profile = await profileRepositoryWrapper.GetProfileAsync(identityCache.Identity!.Address, cts.Token);

                if (profile == null) return;

                userNameElementPresenter.Setup(profile.Value);
                walletAddressElementPresenter.Setup(profile.Value);

                await GetProfileThumbnailCommand.Instance.ExecuteAsync(profileThumbnail, null, profile.Value, ct);
            }
        }
    }
}
