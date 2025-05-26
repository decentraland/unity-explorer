using Cysharp.Threading.Tasks;
using DCL.Friends.UI.BlockUserPrompt;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
using MVC;
using UnityEngine;

namespace DCL.Friends.UI.FriendPanel.Sections.Blocked
{
    public class BlockedSectionController : FriendPanelSectionController<BlockedSectionView, BlockedPanelList, BlockedUserView>
    {
        private static readonly RectOffset CONTEXT_MENU_VERTICAL_LAYOUT_PADDING = new (15, 15, 20, 25);
        private const int CONTEXT_MENU_SEPARATOR_HEIGHT = 20;
        private const int CONTEXT_MENU_ELEMENTS_SPACING = 5;

        private readonly IPassportBridge passportBridge;
        private readonly IMVCManager mvcManager;
        private readonly GenericContextMenu contextMenu;
        private readonly UserProfileContextMenuControlSettings userProfileContextMenuControlSettings;

        private BlockedProfile? lastClickedProfileCtx;

        public BlockedSectionController(BlockedSectionView view,
            IMVCManager mvcManager,
            BlockedPanelList requestManager,
            IPassportBridge passportBridge) : base(view, requestManager)
        {
            this.mvcManager = mvcManager;
            this.passportBridge = passportBridge;

            contextMenu = new GenericContextMenu(view.ContextMenuSettings.ContextMenuWidth, verticalLayoutPadding: CONTEXT_MENU_VERTICAL_LAYOUT_PADDING, elementsSpacing: CONTEXT_MENU_ELEMENTS_SPACING)
                         .AddControl(userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings((_, _) => { }))
                         .AddControl(new SeparatorContextMenuControlSettings(CONTEXT_MENU_SEPARATOR_HEIGHT, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.left, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.right))
                         .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.ViewProfileText, view.ContextMenuSettings.ViewProfileSprite, () => ElementClicked(lastClickedProfileCtx!)));

            requestManager.UnblockClicked += UnblockUserClicked;
            requestManager.ContextMenuClicked += ContextMenuClicked;
            requestManager.NoUserInCollection += ShowEmptyState;
            requestManager.AtLeastOneUserInCollection += HideEmptyState;
        }

        public override void Dispose()
        {
            base.Dispose();
            requestManager.UnblockClicked -= UnblockUserClicked;
            requestManager.ContextMenuClicked -= ContextMenuClicked;
            requestManager.NoUserInCollection -= ShowEmptyState;
            requestManager.AtLeastOneUserInCollection -= HideEmptyState;
        }

        private void ShowEmptyState()
        {
            view.SetEmptyState(true);
            view.SetScrollViewState(false);
        }

        private void HideEmptyState()
        {
            view.SetEmptyState(false);
            view.SetScrollViewState(true);
        }

        private void UnblockUserClicked(BlockedProfile profile) =>
            mvcManager.ShowAsync(BlockUserPromptController.IssueCommand(new BlockUserPromptParams(profile.Address, profile.Name, BlockUserPromptParams.UserBlockAction.UNBLOCK))).Forget();

        private void ContextMenuClicked(BlockedProfile friendProfile, Vector2 buttonPosition, BlockedUserView elementView)
        {
            lastClickedProfileCtx = friendProfile;
            userProfileContextMenuControlSettings.SetInitialData(friendProfile.ToUserData(),
                UserProfileContextMenuControlSettings.FriendshipStatus.DISABLED);
            elementView.CanUnHover = false;
            mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(new GenericContextMenuParameter(contextMenu, buttonPosition,
                           actionOnHide: () => elementView.CanUnHover = true,
                           closeTask: panelLifecycleTask?.Task)))
                      .Forget();
        }

        protected override void ElementClicked(FriendProfile profile) =>
            passportBridge.ShowAsync(profile.Address).Forget();

    }
}
