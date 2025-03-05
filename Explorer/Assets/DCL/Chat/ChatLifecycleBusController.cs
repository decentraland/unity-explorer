using Cysharp.Threading.Tasks;
using DCL.Chat.ChatLifecycleBus;
using MVC;
using System;

namespace DCL.Chat
{
    public class ChatLifecycleBusController : IChatLifecycleBusController, IDisposable
    {
        private readonly IMVCManager mvcManager;

        public event Action? ChatToggleRequested;
        public event Action? ChatHideRequested;

        public ChatLifecycleBusController(IMVCManager mvcManager)
        {
            this.mvcManager = mvcManager;
        }

        public void ShowChat() =>
            mvcManager.ShowAsync(ChatController.IssueCommand()).Forget();

        public void HideChat() =>
            ChatHideRequested?.Invoke();

        public void ToggleChat() =>
            ChatToggleRequested?.Invoke();

        public void Dispose() =>
            ChatHideRequested = null;
    }
}
