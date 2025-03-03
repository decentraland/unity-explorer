using Cysharp.Threading.Tasks;
using DCL.UI;
using System;
using System.Threading;
using UnityEngine;

namespace MVC
{
    public interface IMVCManagerMenusAccessFacade
    {
        UniTask ShowExternalUrlPromptAsync(string url, CancellationToken ct);

        UniTask ShowTeleporterPromptAsync(Vector2Int coords, CancellationToken ct);

        UniTask ShowChangeRealmPromptAsync(string message, string realm, CancellationToken ct);

        UniTask ShowPastePopupToastAsync(PastePopupToastData data, CancellationToken ct);

        UniTask ShowChatEntryMenuPopupAsync(ChatEntryMenuPopupData data, CancellationToken ct);

        UniTask ShowUserProfileContextMenuFromWalletIdAsync(string walletId, Vector3 position, CancellationToken ct, Action onHide = null);

        UniTask ShowUserProfileContextMenuFromUserNameAsync(string userName, Vector3 position, CancellationToken ct);
    }
}
