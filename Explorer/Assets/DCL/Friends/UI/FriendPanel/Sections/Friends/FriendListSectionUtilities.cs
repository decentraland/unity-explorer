using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connectivity;
using DCL.UI.GenericContextMenu.Controls.Configs;
using ECS.SceneLifeCycle.Realm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Friends.UI.FriendPanel.Sections.Friends
{
    public static class FriendListSectionUtilities
    {
        private static readonly RectOffset CONTEXT_MENU_VERTICAL_LAYOUT_PADDING = new (15, 15, 20, 25);
        private const int CONTEXT_MENU_SEPARATOR_HEIGHT = 20;
        private const int CONTEXT_MENU_ELEMENTS_SPACING = 5;

        public static void JumpToFriendLocation(string targetUserAddress,
            CancellationTokenSource? jumpToFriendLocationCts,
            string[] getUserPositionBuffer,
            IOnlineUsersProvider onlineUsersProvider,
            IRealmNavigator realmNavigator,
            Action<Vector2Int>? parcelCalculatedCallback = null)
        {
            jumpToFriendLocationCts = jumpToFriendLocationCts.SafeRestart();
            JumpToFriendLocationAsync(jumpToFriendLocationCts.Token).Forget();
            return;

            async UniTaskVoid JumpToFriendLocationAsync(CancellationToken ct = default)
            {
                getUserPositionBuffer[0] = targetUserAddress;

                IReadOnlyCollection<OnlineUserData> onlineData = await onlineUsersProvider.GetAsync(getUserPositionBuffer, ct);

                if (onlineData.Count == 0)
                    return;

                OnlineUserData userData = onlineData.First();
                Vector2Int parcel = userData.position.ToParcel();
                realmNavigator.TeleportToParcelAsync(parcel, ct, false).Forget();
                parcelCalculatedCallback?.Invoke(parcel);
            }
        }

        internal static (GenericContextMenu, GenericContextMenuElement) BuildContextMenu(
            FriendListContextMenuConfiguration contextMenuSettings,
            UserProfileContextMenuControlSettings userProfileContextMenuControlSettings,
            bool includeUserBlocking,
            Action openProfilePassportCallback,
            Action jumpToFriendCallback,
            Action blockUserCallback)
        {
            GenericContextMenuElement jumpInElement;

            GenericContextMenu contextMenu = new GenericContextMenu(contextMenuSettings.ContextMenuWidth, verticalLayoutPadding: CONTEXT_MENU_VERTICAL_LAYOUT_PADDING, elementsSpacing: CONTEXT_MENU_ELEMENTS_SPACING)
                                            .AddControl(userProfileContextMenuControlSettings)
                                            .AddControl(new SeparatorContextMenuControlSettings(CONTEXT_MENU_SEPARATOR_HEIGHT, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.left, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.right))
                                            .AddControl(new ButtonContextMenuControlSettings(contextMenuSettings.ViewProfileText, contextMenuSettings.ViewProfileSprite, openProfilePassportCallback))
                                            .AddControl(jumpInElement = new GenericContextMenuElement(new ButtonContextMenuControlSettings(contextMenuSettings.JumpToLocationText,
                                                 contextMenuSettings.JumpToLocationSprite, jumpToFriendCallback), false))
                                            .AddControl(new GenericContextMenuElement(new ButtonContextMenuControlSettings(contextMenuSettings.BlockText, contextMenuSettings.BlockSprite, blockUserCallback), includeUserBlocking));

            return (contextMenu, jumpInElement);
        }

        internal static void BlockUserClicked(FriendProfile profile)
        {
            ReportHub.Log(LogType.Error, new ReportData(ReportCategory.FRIENDS), $"Block user button clicked for {profile.Address.ToString()}. Users should not be able to reach this");
        }

        internal static void OpenProfilePassport(FriendProfile profile, IPassportBridge passportBridge) =>
            passportBridge.ShowAsync(profile.Address).Forget();
    }
}
