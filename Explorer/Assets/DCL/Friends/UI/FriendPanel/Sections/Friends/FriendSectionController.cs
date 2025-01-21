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

        private Profile currentProfile;

        public FriendSectionController(FriendsSectionView view,
            IWeb3IdentityCache web3IdentityCache,
            IMVCManager mvcManager,
            ISystemClipboard systemClipboard,
            FriendListRequestManager requestManager) : base(view, web3IdentityCache, requestManager)
        {
            this.mvcManager = mvcManager;

            contextMenu = new GenericContextMenu(view.ContextMenuWidth, verticalLayoutPadding: new RectOffset(15, 15, 20, 25), elementsSpacing: 5)
               .AddControl(userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings(systemClipboard, profile => Debug.Log($"Send friendship request to {profile.UserId}")))
                .AddControl(new SeparatorContextMenuControlSettings(20))
                .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuViewProfileText, view.ContextMenuViewProfileSprite, () => OpenProfilePassport(currentProfile!)))
                .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuChatText, view.ContextMenuChatSprite, () => Debug.Log($"Chat with {currentProfile!.UserId}")))
                .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuJumpInText, view.ContextMenuJumpInSprite, () => Debug.Log($"Jump to {currentProfile!.UserId} location")))
                .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuBlockText, view.ContextMenuBlockSprite, () => Debug.Log($"Block {currentProfile!.UserId}")))
                .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuReportText, view.ContextMenuReportSprite, () => Debug.Log($"Report {currentProfile!.UserId}")));

            requestManager.ContextMenuClicked += ContextMenuClicked;
        }

        public override void Dispose()
        {
            base.Dispose();
            requestManager.ContextMenuClicked -= ContextMenuClicked;
        }

        private void ContextMenuClicked(Profile profile, Vector2 buttonPosition)
        {
            currentProfile = profile;
            userProfileContextMenuControlSettings.SetInitialData(profile, view.ChatEntryConfiguration.GetNameColor(profile.Name), UserProfileContextMenuControlSettings.FriendshipStatus.FRIEND);
            mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(new GenericContextMenuParameter(contextMenu, buttonPosition))).Forget();
        }

        private void OpenProfilePassport(Profile profile) =>
            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportController.Params(profile.UserId))).Forget();

        protected override void ElementClicked(Profile profile) =>
            OpenProfilePassport(profile);
    }
}
