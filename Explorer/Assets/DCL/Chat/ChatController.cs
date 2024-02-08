using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using MVC;
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
            ChatEntryConfigurationSO chatEntryConfiguration,
            IDebugContainerBuilder debugBuilder) : base(viewFactory)
        {
            this.chatEntryView = chatEntryView;
            this.chatEntryConfiguration = chatEntryConfiguration;

            debugBuilder.AddWidget("Chat").AddControl(new DebugButtonDef("Create chat message", CreateChatEntry), null);
        }

        protected override void OnViewInstantiated()
        {
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
            viewInstance.StopChatEntriesFadeout();
        }

        private void OnInputChanged(string inputText)
        {
            viewInstance.CharacterCounter.SetCharacterCount(inputText.Length);
            viewInstance.StopChatEntriesFadeout();
        }

        private void CreateChatEntry()
        {
            ChatEntryView entryView = Object.Instantiate(chatEntryView, viewInstance.MessagesContainer);
            entryView.Initialise(chatEntryConfiguration);
            entryView.SetUsername("User" + UnityEngine.Random.Range(0, 3), UnityEngine.Random.Range(0, 2) == 0 ? "" : "#asd38");
            entryView.entryText.text = GenerateRandomString(UnityEngine.Random.Range(5, 200));
            entryView.SetSentByUser(UnityEngine.Random.Range(0, 10) <= 2);
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
