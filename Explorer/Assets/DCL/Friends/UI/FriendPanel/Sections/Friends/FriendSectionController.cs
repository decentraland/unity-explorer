using Cysharp.Threading.Tasks;
using DCL.Clipboard;
using DCL.Passport;
using DCL.Profiles;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.Web3.Identities;
using MVC;
using UnityEngine;

namespace DCL.Friends.UI.FriendPanel.Sections.Friends
{
    public class FriendSectionController : FriendPanelSectionController<FriendsSectionView, FriendListRequestManager, FriendListUserView>
    {
        private readonly IMVCManager mvcManager;
        private readonly IFriendsService friendsService;
        private readonly GenericContextMenu contextMenu;
        private readonly UserProfileContextMenuControlSettings userProfileContextMenuControlSettings;

        public FriendSectionController(FriendsSectionView view,
            IWeb3IdentityCache web3IdentityCache,
            IMVCManager mvcManager,
            ISystemClipboard systemClipboard,
            FriendListRequestManager requestManager) : base(view, web3IdentityCache, requestManager)
        {
            this.mvcManager = mvcManager;

            contextMenu = new GenericContextMenu(view.ContextMenuSettings.ContextMenuWidth, verticalLayoutPadding: new RectOffset(15, 15, 20, 25), elementsSpacing: 5)
               .AddControl(userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings(systemClipboard, profile => Debug.Log($"Send friendship request to {profile.UserId}")))
                .AddControl(new SeparatorContextMenuControlSettings(20, -15, -15))
                .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.ViewProfileText, view.ContextMenuSettings.ViewProfileSprite, () => OpenProfilePassport(lastClickedProfile!)))
                .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.BlockText, view.ContextMenuSettings.BlockSprite, () => Debug.Log($"Block {lastClickedProfile!.UserId}")))
                .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.ReportText, view.ContextMenuSettings.ReportSprite, () => Debug.Log($"Report {lastClickedProfile!.UserId}")));

            requestManager.ContextMenuClicked += ContextMenuClicked;
        }

        public override void Dispose()
        {
            base.Dispose();
            requestManager.ContextMenuClicked -= ContextMenuClicked;
        }

        private void ContextMenuClicked(Profile profile, Vector2 buttonPosition)
        {
            userProfileContextMenuControlSettings.SetInitialData(profile, view.ChatEntryConfiguration.GetNameColor(profile.Name), UserProfileContextMenuControlSettings.FriendshipStatus.FRIEND);
            mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(new GenericContextMenuParameter(contextMenu, buttonPosition))).Forget();
        }

        private void OpenProfilePassport(Profile profile) =>
            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportController.Params(profile.UserId))).Forget();

        protected override void ElementClicked(Profile profile) =>
            OpenProfilePassport(profile);
    }
}
