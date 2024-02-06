using Cysharp.Threading.Tasks;
using MVC;
using System;
using System.Linq;
using System.Threading;
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
            viewInstance.CharacterCounter.SetMaximumLength(viewInstance.InputField.characterLimit);
            viewInstance.CharacterCounter.gameObject.SetActive(false);
            viewInstance.InputField.onValueChanged.AddListener(OnInputChanged);
            viewInstance.InputField.onSelect.AddListener(OnInputSelected);
            viewInstance.InputField.onDeselect.AddListener(OnInputDeselected);
        }

        private void OnInputDeselected(string inputText)
        {
            viewInstance.CharacterCounter.gameObject.SetActive(false);
        }

        private void OnInputSelected(string inputText)
        {
            viewInstance.CharacterCounter.gameObject.SetActive(true);
        }

        private void OnInputChanged(string inputText)
        {
            viewInstance.CharacterCounter.SetCharacterCount(inputText.Length);
        }

        private async UniTaskVoid CreateChatEntries()
        {
            do
            {
                ChatEntryView entryView = Object.Instantiate(chatEntryView, viewInstance.MessagesContainer);
                entryView.Initialise(chatEntryConfiguration);
                entryView.SetUsername(
                    "User" + UnityEngine.Random.Range(0, 100),
                    UnityEngine.Random.Range(0, 2) == 0 ? "" : "#asd38");
                entryView.entryText.text = GenerateRandomString(UnityEngine.Random.Range(5, 200));
                entryView.SetSentByUser(UnityEngine.Random.Range(0, 10) <= 2);
                await UniTask.Delay(UnityEngine.Random.Range(2000, 6000));
            }
            while (true);
        }

        private string GenerateRandomString(int length)
        {
            const string chars = " ABCDEFGHIJ KLMNOPQRSTU VWXYZ0123456789 ";
            return new string(Enumerable.Repeat(chars, length).Select(s => s[UnityEngine.Random.Range(0, s.Length)]).ToArray());
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
