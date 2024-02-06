using Cysharp.Threading.Tasks;
using MVC;
using System;
using System.Linq;
using System.Threading;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Chat
{
    public partial class ChatController : ControllerBase<ChatView>
    {
        private readonly ChatEntryView chatEntryView;
        private readonly ChatEntryConfigurationSO chatEntryConfiguration;
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public ChatController(
            ViewFactoryMethod viewFactory,
            ChatEntryView chatEntryView,
            ChatEntryConfigurationSO chatEntryConfiguration) : base(viewFactory)
        {
            this.chatEntryView = chatEntryView;
            this.chatEntryConfiguration = chatEntryConfiguration;
        }

        protected override void OnViewInstantiated()
        {
            CreateChatEntries().Forget();
        }

        private async UniTaskVoid CreateChatEntries()
        {
            do
            {
                ChatEntryView entryView = Object.Instantiate(chatEntryView, viewInstance.MessagesContainer);
                entryView.Initialise(chatEntryConfiguration);
                string username = "User" + UnityEngine.Random.Range(0, 100);
                string walletId = UnityEngine.Random.Range(0, 2) == 0 ? "" : "#asd38";
                entryView.SetUsername(username, walletId);
                entryView.entryText.text = GenerateRandomString(UnityEngine.Random.Range(5, 200));
                entryView.SetSentByUser(UnityEngine.Random.Range(0, 10) <= 2);
                await UniTask.Delay(UnityEngine.Random.Range(2000, 6000));
            }
            while (true);
        }

        private string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length).Select(s => s[UnityEngine.Random.Range(0, s.Length)]).ToArray());
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
