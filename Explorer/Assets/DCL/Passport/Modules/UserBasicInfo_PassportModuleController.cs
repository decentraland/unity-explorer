using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI.ProfileElements;
using DCL.UI.ProfileNames;
using DCL.Web3;
using MVC;
using System;
using System.Threading;
using Utility;

namespace DCL.Passport.Modules
{
    public class UserBasicInfo_PassportModuleController : IPassportModuleController
    {
        private readonly UserNameElementController nameElementController;
        private readonly UserWalletAddressElementController walletAddressElementController;
        private readonly UserBasicInfo_PassportModuleView view;
        private readonly ISelfProfile selfProfile;
        private readonly IWebBrowser webBrowser;
        private readonly IMVCManager mvcManager;
        private readonly INftNamesProvider nftNamesProvider;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly bool isNameEditorEnabled;

        private CancellationTokenSource? checkNameEditionCancellationToken;
        private CancellationTokenSource? showNameEditorCancellationToken;
        private Profile? currentProfile;

        public event Action? NameClaimRequested;

        public UserBasicInfo_PassportModuleController(
            UserBasicInfo_PassportModuleView view,
            ISelfProfile selfProfile,
            IWebBrowser webBrowser,
            IMVCManager mvcManager,
            INftNamesProvider nftNamesProvider,
            IDecentralandUrlsSource decentralandUrlsSource,
            bool isNameEditorEnabled)
        {
            this.view = view;
            this.selfProfile = selfProfile;
            this.webBrowser = webBrowser;
            this.mvcManager = mvcManager;
            this.nftNamesProvider = nftNamesProvider;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.isNameEditorEnabled = isNameEditorEnabled;
            nameElementController = new UserNameElementController(view.UserNameElement);
            walletAddressElementController = new UserWalletAddressElementController(view.UserWalletAddressElement);

            view.ClaimNameButton.onClick.AddListener(ClaimName);
            view.EditNameButton.onClick.AddListener(ShowNameEditor);
        }

        public void Setup(Profile profile)
        {
            currentProfile = profile;

            nameElementController.Setup(profile);
            walletAddressElementController.Setup(profile);

            checkNameEditionCancellationToken = checkNameEditionCancellationToken.SafeRestart();
            CheckForEditionAvailabilityAsync(checkNameEditionCancellationToken.Token).Forget();
        }

        public void Clear()
        {
            nameElementController.Element.CopyNameWarningNotification.Hide(true);
            walletAddressElementController.Element.CopyWalletWarningNotification.Hide(true);
        }

        public void Dispose()
        {
            nameElementController.Element.CopyUserNameButton.onClick.RemoveAllListeners();
            walletAddressElementController.Element.CopyWalletAddressButton.onClick.RemoveAllListeners();
            Clear();
        }

        private async UniTaskVoid CheckForEditionAvailabilityAsync(CancellationToken ct)
        {
            try
            {
                view.EditNameButton.gameObject.SetActive(false);
                view.ClaimNameButton.gameObject.SetActive(false);

                Profile? ownProfile = await selfProfile.ProfileAsync(ct);

                if (ownProfile == null) return;

                if (ownProfile.UserId == currentProfile?.UserId)
                {
                    view.EditNameButton.gameObject.SetActive(isNameEditorEnabled);
                    view.ClaimNameButton.gameObject.SetActive(false);

                    if (isNameEditorEnabled)
                    {
                        using INftNamesProvider.PaginatedNamesResponse names = await nftNamesProvider.GetAsync(new Web3Address(currentProfile.UserId), 1, 1, ct);
                        view.ClaimNameButton.gameObject.SetActive(names.TotalAmount <= 0);
                    }
                    else
                        view.ClaimNameButton.gameObject.SetActive(false);
                }
                else
                {
                    view.EditNameButton.gameObject.SetActive(false);
                    view.ClaimNameButton.gameObject.SetActive(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.PROFILE); }
        }

        private void ShowNameEditor()
        {
            if (currentProfile == null) return;

            showNameEditorCancellationToken = showNameEditorCancellationToken.SafeRestart();
            ShowNameEditorAsync(showNameEditorCancellationToken.Token).Forget();
            return;

            async UniTaskVoid ShowNameEditorAsync(CancellationToken ct)
            {
                await mvcManager.ShowAsync(ProfileNameEditorController.IssueCommand(), ct);

                Profile? profile = await selfProfile.ProfileAsync(ct);

                // Re-configure ui
                if (profile != null)
                    Setup(profile);
            }
        }

        private void ClaimName()
        {
            webBrowser.OpenUrl(decentralandUrlsSource.Url(DecentralandUrl.MarketplaceClaimName));
            NameClaimRequested?.Invoke();
        }
    }
}
