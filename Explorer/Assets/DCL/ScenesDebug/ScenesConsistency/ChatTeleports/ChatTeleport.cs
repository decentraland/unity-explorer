using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.ScenesDebug.ScenesConsistency.ChatTeleports;
using DCL.Utilities.Extensions;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.ScenesDebug.ScenesConsistency
{
    public class ChatTeleport : IChatTeleport
    {
        private ChatView? chatView;

        public UniTask WaitReadyAsync() =>
            UniTask.WaitUntil(() =>
            {
                chatView = Object.FindObjectOfType<ChatView>();
                return chatView != null;
            });

        public void GoTo(Vector2Int coordinate)
        {
            var field = chatView.EnsureNotNull().InputField;
            field.text = $"/goto {coordinate.x},{coordinate.y}";
            field.OnSubmit(new BaseEventData(null!));
        }
    }
}
