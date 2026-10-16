using Cysharp.Threading.Tasks;
using DCL.Credits;
using DCL.FeatureFlags;
using DCL.MarketplaceCredits;
using DCL.MarketplaceCredits.Purchase;
using DCL.MarketplaceCredits.Purchase.TopUp.UI;
using DCL.Profiles;
using DCL.UI.Credits;
using DCL.UI.UpgradeGuestAccountPopup;
using DCL.Web3.Identities;
using MVC;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    ///     Shared setup of the credits panel that several fullscreen panels embed in their top bar.
    /// </summary>
    public static class CreditsPanelSetup
    {
        /// <summary>
        ///     Keeps the view hidden unless the credits feature is on and the user is allowed, in which case it returns a live controller.
        /// </summary>
        public static async UniTask<ICreditsPanelController> EnableIfUserAllowedAsync(
            ICreditsPanelView view,
            MarketplaceCreditsAPIClient creditsAPIClient,
            ProfileChangesBus profileChangesBus,
            IWeb3IdentityCache identityCache,
            IMVCManager mvcManager,
            CancellationToken ct)
        {
            view.IsShown = false;

            if (!FeaturesRegistry.Instance.IsEnabled(FeatureId.UserCredits))
                return new NullCreditsPanelController();

            if (!await CreditsFeatureAccess.Instance.IsUserAllowedToUseTheFeatureAsync(ct))
                return new NullCreditsPanelController();

            var controller = new CreditsPanelController(view, creditsAPIClient, profileChangesBus, identityCache,
                topUpEnabled: FeaturesRegistry.Instance.IsEnabled(FeatureId.CreditsTopup),
                openTopUpPanel: OpenTopUpPanel);

            view.IsShown = true;

            return controller;

            void OpenTopUpPanel()
            {
                if (identityCache.IsGuest())
                {
                    mvcManager.ShowAndForget(UpgradeGuestAccountPopupController.IssueCommand(new UpgradeGuestAccountPopupController.Params(GuestUpgradeTrigger.Credits)), ct: ct);
                    return;
                }

                mvcManager.ShowAsync(CreditsTopUpModalController.IssueCommand(new CreditsTopUpModalControllerParams(CreditsTopUpModalControllerParams.SOURCE_HUD)), ct).Forget();
            }
        }
    }
}
