using Cysharp.Threading.Tasks;
using DCL.Chat.ChatLifecycleBus;
using MVC;
using System;

namespace DCL.Chat
{
    public class ChatLifecycleBusController : IChatLifecycleBusController, IDisposable
    {
        private readonly IMVCManager mvcManager;

        private event Action? ChatHideAction;

        public ChatLifecycleBusController(IMVCManager mvcManager)
        {
            this.mvcManager = mvcManager;
        }

        public void ShowChat() =>
            mvcManager.ShowAsync(ChatController.IssueCommand()).Forget();

        public void HideChat() =>
            ChatHideAction?.Invoke();

        public void SubscribeToHideChatCommand(Action action) =>
            ChatHideAction += action;

        public void Dispose() =>
            ChatHideAction = null;
    }
}
