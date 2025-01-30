using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Clipboard;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.Web3.Identities;
using MVC;
using UnityEngine;

namespace DCL.Friends.UI.FriendPanel.Sections.Friends
{
    public class FriendSectionController : FriendPanelSectionController<FriendsSectionView, FriendListRequestManager, FriendListUserView>
    {
        private static readonly RectOffset CONTEXT_MENU_VERTICAL_LAYOUT_PADDING = new (15, 15, 20, 25);
        private const int CONTEXT_MENU_SEPARATOR_HEIGHT = 20;
        private const int CONTEXT_MENU_ELEMENTS_SPACING = 5;

        private readonly IMVCManager mvcManager;
        private readonly IPassportBridge passportBridge;
        private readonly GenericContextMenu contextMenu;
        private readonly UserProfileContextMenuControlSettings userProfileContextMenuControlSettings;
        private readonly IProfileThumbnailCache profileThumbnailCache;

        private FriendProfile? lastClickedProfileCtx;

        public FriendSectionController(FriendsSectionView view,
            IWeb3IdentityCache web3IdentityCache,
            IMVCManager mvcManager,
            ISystemClipboard systemClipboard,
            FriendListRequestManager requestManager,
            IPassportBridge passportBridge,
            IWebBrowser webBrowser,
            IProfileThumbnailCache profileThumbnailCache) : base(view, web3IdentityCache, requestManager)
        {
            this.mvcManager = mvcManager;
            this.passportBridge = passportBridge;
            this.profileThumbnailCache = profileThumbnailCache;

            contextMenu = new GenericContextMenu(view.ContextMenuSettings.ContextMenuWidth, verticalLayoutPadding: CONTEXT_MENU_VERTICAL_LAYOUT_PADDING, elementsSpacing: CONTEXT_MENU_ELEMENTS_SPACING)
                         .AddControl(userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings(systemClipboard, userId => Debug.Log($"Send friendship request to {userId}")))
                         .AddControl(new SeparatorContextMenuControlSettings(CONTEXT_MENU_SEPARATOR_HEIGHT, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.left, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.right))
                         .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.ViewProfileText, view.ContextMenuSettings.ViewProfileSprite, () => OpenProfilePassport(lastClickedProfileCtx!)))
                         .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.ViewProfileText, view.ContextMenuSettings.ViewProfileSprite, () => OpenProfilePassport(lastClickedProfileCtx!)))
                         .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.BlockText, view.ContextMenuSettings.BlockSprite, () => Debug.Log($"Block {lastClickedProfileCtx!.Address.ToString()}")))
                         .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.ReportText, view.ContextMenuSettings.ReportSprite, () => FriendsCommonActions.ReportPlayer(webBrowser)));

            requestManager.ContextMenuClicked += ContextMenuClicked;
        }

        public override void Dispose()
        {
            base.Dispose();
            requestManager.ContextMenuClicked -= ContextMenuClicked;
        }

        private void ContextMenuClicked(FriendProfile friendProfile, Vector2 buttonPosition, FriendListUserView elementView)
        {
            lastClickedProfileCtx = friendProfile;
            userProfileContextMenuControlSettings.SetInitialData(friendProfile.Name, friendProfile.Address, friendProfile.HasClaimedName,
                view.ChatEntryConfiguration.GetNameColor(friendProfile.Name), UserProfileContextMenuControlSettings.FriendshipStatus.FRIEND,
                profileThumbnailCache.GetThumbnail(friendProfile.Address.ToString()));
            elementView.CanUnHover = false;
            mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(new GenericContextMenuParameter(contextMenu, buttonPosition, actionOnHide: () => elementView.CanUnHover = true))).Forget();
        }

        private void OpenProfilePassport(FriendProfile profile) =>
            passportBridge.ShowAsync(profile.Address).Forget();

        protected override void ElementClicked(FriendProfile profile) =>
            OpenProfilePassport(profile);
    }
}
