using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.CommunicationData.URLHelpers;
using DCL.Friends.UI.BlockUserPrompt;
using DCL.Multiplayer.Connectivity;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.Web3;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Friends.UI.FriendPanel.Sections.Friends
{
    public static class FriendListSectionUtilities
    {
        private const string WORLDS_BASE_URL = "https://worlds-content-server.decentraland.org/world/";
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

                if (userData.IsInWorld)
                {
                    realmNavigator.TryChangeRealmAsync(URLDomain.FromString(new ENS(userData.worldName).ConvertEnsToWorldUrl()), ct).Forget();
                }
                else
                {
                    Vector2Int parcel = userData.position.ToParcel();
                    realmNavigator.TeleportToParcelAsync(parcel, ct, false).Forget();
                    parcelCalculatedCallback?.Invoke(parcel);
                }
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

        public static void BlockUserClicked(IMVCManager mvcManager, Web3Address targetUserAddress, string targetUserName) =>
            mvcManager.ShowAsync(BlockUserPromptController.IssueCommand(new BlockUserPromptParams(targetUserAddress, targetUserName, BlockUserPromptParams.UserBlockAction.BLOCK))).Forget();

        internal static void OpenProfilePassport(FriendProfile profile, IPassportBridge passportBridge) =>
            passportBridge.ShowAsync(profile.Address).Forget();

        internal static string FormatDate(DateTime date)
        {
            Span<char> buffer = stackalloc char[6];
            if (!date.TryFormat(buffer, out _, "MMM dd", CultureInfo.InvariantCulture)) return string.Empty;

            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = char.ToUpperInvariant(buffer[i]);

            return buffer.ToString();
        }
    }
}
