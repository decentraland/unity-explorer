using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.UI;
using DCL.Web3;
using System;
using System.Threading;
using UnityEngine;

namespace MVC
{
    public interface IMVCManagerMenusAccessFacade
    {
        UniTask ShowExternalUrlPromptAsync(URLAddress url, CancellationToken ct);

        UniTask ShowTeleporterPromptAsync(Vector2Int coords, CancellationToken ct);

        UniTask ShowChangeRealmPromptAsync(string message, string realm, CancellationToken ct);

        UniTask ShowPastePopupToastAsync(PastePopupToastData data, CancellationToken ct);

        UniTask ShowChatEntryMenuPopupAsync(ChatEntryMenuPopupData data, CancellationToken ct);

        UniTask ShowUserProfileContextMenuFromWalletIdAsync(Web3Address walletId, Vector3 position, CancellationToken ct, Action onHide = null);

        UniTask ShowUserProfileContextMenuFromUserNameAsync(string userName, Vector3 position, CancellationToken ct, Action onHide = null);

        UniTaskVoid ShowChatContextMenuAsync(bool chatBubblesVisibility, Vector3 transformPosition, ChatOptionsContextMenuData data, Action<bool> onToggleChatBubblesVisibility, Action onContextMenuHide);
    }

    [Serializable]
    public struct ChatOptionsContextMenuData
    {
        public string ChatBubblesToggleText;
        public Sprite ChatBubblesToggleIcon;
        public string PinChatToggleText;
        public Sprite PinChatToggleTextIcon;
    }
}
