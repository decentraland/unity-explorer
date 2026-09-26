using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

namespace DCL.UI
{
    public struct ChatEntryMenuPopupData
    {
        public readonly string CopiedText;
        public readonly UniTask? CloseTask;
        public readonly Vector2 Position;
        public readonly Action OnPopupClose;

        public ChatEntryMenuPopupData(Vector2 position, string copiedText, Action onPopupClose, UniTask? closeTask = null)
        {
            Position = position;
            CopiedText = copiedText;
            OnPopupClose = onPopupClose;
            CloseTask = closeTask;
        }
    }
}
