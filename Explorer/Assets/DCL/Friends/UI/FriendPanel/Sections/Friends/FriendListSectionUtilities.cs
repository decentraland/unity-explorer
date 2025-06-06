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
using Utility.Types;

namespace DCL.Friends.UI.FriendPanel.Sections.Friends
{
    // TODO: move to realm navigator
    public enum JumpToFriendErrorKind
    {
        ChangeRealm,
        TeleportParcel
    }

    // TODO: move to realm navigator?
    public struct JumpToFriendErrorInfo
    {
        public JumpToFriendErrorKind Kind;
        public ChangeRealmError? RealmError;
        public string Origin { get; set; }
        public string RealmErrorMessage { get; set; }
        public string TeleportErrorMessage { get; set; }
        
        public Utility.Types.TaskError? TeleportError;
    }
    
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
            Action<Vector2Int>? parcelCalculatedCallback = null,
            Action<JumpToFriendErrorInfo>?  onError = null)
        {
            jumpToFriendLocationCts = jumpToFriendLocationCts.SafeRestart();
            JumpToFriendLocationAsync(jumpToFriendLocationCts.Token).Forget();
            return;

            async UniTaskVoid JumpToFriendLocationAsync(CancellationToken ct = default)
            {
                getUserPositionBuffer[0] = targetUserAddress;
                IReadOnlyCollection<OnlineUserData> onlineData = 
                    await onlineUsersProvider.GetAsync(getUserPositionBuffer, ct);

                if (onlineData.Count == 0)
                    return;

                OnlineUserData userData = onlineData.First();

                if (userData.IsInWorld)
                {
                    var result  = await realmNavigator
                        .TryChangeRealmAsync(
                            URLDomain.FromString(new ENS(userData.worldName).ConvertEnsToWorldUrl()),
                            ct);
                    
                    if (!result.Success && result.Error.HasValue)
                    {
                        // TODO create error info somewhere else
                        // TODO maybe in realm navigator?
                        onError?.Invoke(new JumpToFriendErrorInfo
                        {
                            Kind = JumpToFriendErrorKind.ChangeRealm,
                            RealmError = result.Error.Value.State,
                            RealmErrorMessage = GetRealmErrorMessage(result.Error.Value, userData.worldName),
                            TeleportError = null,
                            TeleportErrorMessage = string.Empty
                        });
                    }
                }
                else
                {
                    var parcel = userData.position.ToParcel();
                    var result = await realmNavigator.TeleportToParcelAsync(parcel, ct, false);
                    if (!result.Success && result.Error.HasValue)
                    {
                        // TODO create error info somewhere else
                        // TODO maybe in realm navigator?
                        onError?.Invoke(new JumpToFriendErrorInfo
                        {
                            Kind = JumpToFriendErrorKind.TeleportParcel,
                            RealmError = null,
                            RealmErrorMessage = string.Empty,
                            TeleportError = result.Error.Value.State,
                            TeleportErrorMessage = GetTeleportErrorMessage(result.Error.Value, parcel.ToString())
                        });
                    }

                    parcelCalculatedCallback?.Invoke(parcel);
                }
            }
        }

        private static string GetTeleportErrorMessage((TaskError State, string Message, Exception Exception) error, string destination)
        {
            return error.State switch
            {
                TaskError.MessageError => $"🔴 Error. Teleport to parcel {destination} failed",
                TaskError.Timeout => "🔴 Error. Timeout",
                TaskError.Cancelled => "🔴 Error. The operation was canceled!",
                TaskError.UnexpectedException => $"🔴 Error. Teleport to {destination} failed",
                _ => "🔴 Unknown Error. The operation was canceled!"
            };
        }

        private static string GetRealmErrorMessage((ChangeRealmError State, string Message, Exception Exception) error,string destination)
        {
            return error.State switch
            {
                ChangeRealmError.MessageError => $"🔴 Teleport was not fully successful to {destination} world!",
                ChangeRealmError.SameRealm => $"🟡 You are already in {destination}!",
                ChangeRealmError.NotReachable => $"🔴 Error. The world {destination} doesn't exist or not reachable!",
                ChangeRealmError.ChangeCancelled => "🔴 Error. The operation was canceled!",
                _ => "🔴 Unknown Error. The operation was canceled!"
            };
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
