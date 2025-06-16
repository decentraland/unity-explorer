using Cysharp.Threading.Tasks;
using DCL.Friends.UI.BlockUserPrompt;
using DCL.Multiplayer.Connectivity;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.Web3;
using MVC;
using System;
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

        /// <summary>
        /// Fetches the OnlineUserData for `userId`, restarts the CTS, 
        /// and returns (success, isInWorld, parameters, parcel).
        /// </summary>
        public static async UniTask<(bool success, bool isInWorld, string parameters, Vector2Int? parcel)> 
            PrepareTeleportTargetAsync(string userId,
                IOnlineUsersProvider onlineUsersProvider,
                CancellationTokenSource? ct)
        {
            ct = ct.SafeRestart();
            var onlineData = await onlineUsersProvider.GetAsync(new [] { userId }, ct.Token);
            if (onlineData.Count == 0)
                return (false, false, null!, null);

            var userData = onlineData.First();

            if (userData.IsInWorld)
            {
                // NOTE: Realm
                return (true, true,
                    userData.worldName!,
                    null);
            }
            else
            {
                // NOTE: Parcel
                var p = userData.position.ToParcel();
                return (true, false,
                    $"{p.x},{p.y}",
                    p);
            }
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
