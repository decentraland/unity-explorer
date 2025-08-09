using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.UI;
using DCL.Web3;
using System;
using System.Threading;
using DCL.UI.GenericContextMenuParameter;
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

        UniTask ShowUserProfileContextMenuFromWalletIdAsync(Web3Address walletId, Vector3 position, Vector2 offset, CancellationToken ct, UniTask closeMenuTask, Action onHide = null, MenuAnchorPoint anchorPoint = MenuAnchorPoint.DEFAULT);

        UniTask ShowUserProfileContextMenuFromUserNameAsync(string userName, Vector3 position, Vector2 offset, CancellationToken ct, UniTask closeMenuTask, Action onHide = null);

        UniTaskVoid ShowChatContextMenuAsync(Vector3 transformPosition, ChatOptionsContextMenuData data, Action onDeleteChatHistoryClicked, Action onContextMenuHide, UniTask closeMenuTask);

        UniTask ShowGenericContextMenuAsync(GenericContextMenuParameter parameter);
    }

    [Serializable]
    public struct ChatOptionsContextMenuData
    {
        public string DeleteChatHistoryText;
        public Sprite DeleteChatHistoryIcon;
    }

    public struct CommunityContextMenuData
    {
        public string ViewCommunityText;
        public Sprite ViewCommunityIcon;
        public Action OnViewCommunityClicked;
    }

    public enum MenuAnchorPoint
    {
        TOP_LEFT,
        TOP_RIGHT,
        BOTTOM_LEFT,
        BOTTOM_RIGHT,
        CENTER_LEFT,
        CENTER_RIGHT,
        DEFAULT
    }

}
