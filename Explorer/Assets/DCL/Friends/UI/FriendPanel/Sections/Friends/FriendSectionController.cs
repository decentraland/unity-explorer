using Cysharp.Threading.Tasks;
using DCL.Clipboard;
using DCL.Multiplayer.Connectivity;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.Web3.Identities;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Friends.UI.FriendPanel.Sections.Friends
{
    public class FriendSectionController : FriendPanelSectionController<FriendsSectionView, FriendListRequestManager, FriendListUserView>
    {
        private static readonly RectOffset CONTEXT_MENU_VERTICAL_LAYOUT_PADDING = new (15, 15, 20, 25);
        private const int CONTEXT_MENU_SEPARATOR_HEIGHT = 20;
        private const int CONTEXT_MENU_ELEMENTS_SPACING = 5;

        private readonly IMVCManager mvcManager;
        private readonly IPassportBridge passportBridge;
        private readonly IProfileThumbnailCache profileThumbnailCache;
        private readonly IFriendsService friendsService;
        private readonly UserProfileContextMenuControlSettings userProfileContextMenuControlSettings;
        private readonly IOnlineUsersProvider onlineUsersProvider;
        private readonly IRealmNavigator realmNavigator;
        private readonly bool includeUserBlocking;
        private readonly string[] getUserPositionBuffer = new string[1];

        private CancellationTokenSource? friendshipOperationCts;
        private CancellationTokenSource? jumpToFriendLocationCts;

        public FriendSectionController(FriendsSectionView view,
            IWeb3IdentityCache web3IdentityCache,
            IMVCManager mvcManager,
            ISystemClipboard systemClipboard,
            FriendListRequestManager requestManager,
            IPassportBridge passportBridge,
            IProfileThumbnailCache profileThumbnailCache,
            IFriendsService friendsService,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator,
            bool includeUserBlocking) : base(view, web3IdentityCache, requestManager)
        {
            this.mvcManager = mvcManager;
            this.passportBridge = passportBridge;
            this.profileThumbnailCache = profileThumbnailCache;
            this.friendsService = friendsService;
            this.onlineUsersProvider = onlineUsersProvider;
            this.includeUserBlocking = includeUserBlocking;
            this.realmNavigator = realmNavigator;

            userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings(systemClipboard, HandleContextMenuUserProfileButton);

            requestManager.ContextMenuClicked += ContextMenuClicked;
            requestManager.JumpInClicked += JumpInClicked;
        }

        public override void Dispose()
        {
            base.Dispose();
            requestManager.ContextMenuClicked -= ContextMenuClicked;
            requestManager.JumpInClicked -= JumpInClicked;
            friendshipOperationCts.SafeCancelAndDispose();
            jumpToFriendLocationCts.SafeCancelAndDispose();
        }

        private void HandleContextMenuUserProfileButton(string userId, UserProfileContextMenuControlSettings.FriendshipStatus friendshipStatus)
        {
            friendshipOperationCts = friendshipOperationCts.SafeRestart();
            DeleteFriendshipAsync(friendshipOperationCts.Token).Forget();
            return;

            async UniTaskVoid DeleteFriendshipAsync(CancellationToken ct)
            {
                await friendsService.DeleteFriendshipAsync(userId, ct);
            }
        }

        private void ContextMenuClicked(FriendProfile friendProfile, Vector2 buttonPosition, FriendListUserView elementView)
        {
            userProfileContextMenuControlSettings.SetInitialData(friendProfile.Name, friendProfile.Address, friendProfile.HasClaimedName,
                view.ChatEntryConfiguration.GetNameColor(friendProfile.Name), UserProfileContextMenuControlSettings.FriendshipStatus.FRIEND,
                profileThumbnailCache.GetThumbnail(friendProfile.Address.ToString()));

            elementView.CanUnHover = false;

            mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(
                           new GenericContextMenuParameter(
                               config: BuildContextMenu(friendProfile),
                               anchorPosition: buttonPosition,
                               actionOnHide: () => elementView.CanUnHover = true,
                               closeTask: panelLifecycleTask?.Task))
                       )
                      .Forget();
        }

        private GenericContextMenu BuildContextMenu(FriendProfile friendProfile)
        {
            GenericContextMenu contextMenu = new GenericContextMenu(view.ContextMenuSettings.ContextMenuWidth, verticalLayoutPadding: CONTEXT_MENU_VERTICAL_LAYOUT_PADDING, elementsSpacing: CONTEXT_MENU_ELEMENTS_SPACING)
                                            .AddControl(userProfileContextMenuControlSettings)
                                            .AddControl(new SeparatorContextMenuControlSettings(CONTEXT_MENU_SEPARATOR_HEIGHT, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.left, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.right))
                                            .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.ViewProfileText, view.ContextMenuSettings.ViewProfileSprite, () => OpenProfilePassport(friendProfile)));

            if (requestManager.IsFriendInGame(friendProfile))
                contextMenu.AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.JumpToLocationText, view.ContextMenuSettings.JumpToLocationSprite, () => JumpToFriendLocation(friendProfile)));

            if (includeUserBlocking)
                contextMenu.AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.BlockText, view.ContextMenuSettings.BlockSprite, () => Debug.Log($"Block {friendProfile.Address.ToString()}")));

            return contextMenu;
        }

        private void JumpToFriendLocation(FriendProfile profile)
        {
            jumpToFriendLocationCts = jumpToFriendLocationCts.SafeRestart();
            JumpToFriendLocationAsync(jumpToFriendLocationCts.Token).Forget();
            return;

            async UniTaskVoid JumpToFriendLocationAsync(CancellationToken ct = default)
            {
                getUserPositionBuffer[0] = profile.Address.ToString();

                IReadOnlyCollection<OnlineUserData> onlineData = await onlineUsersProvider.GetAsync(getUserPositionBuffer, ct);

                if (onlineData.Count == 0)
                    return;

                OnlineUserData userData = onlineData.First();
                Vector2Int parcel = userData.position.ToParcel();
                realmNavigator.TeleportToParcelAsync(parcel, ct, false).Forget();
            }
        }

        private void JumpInClicked(FriendProfile profile) =>
            JumpToFriendLocation(profile);

        private void OpenProfilePassport(FriendProfile profile) =>
            passportBridge.ShowAsync(profile.Address).Forget();

        protected override void ElementClicked(FriendProfile profile) =>
            OpenProfilePassport(profile);
    }
}
