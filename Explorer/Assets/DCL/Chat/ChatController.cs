using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;
using TMPro;

namespace DCL.Chat
{
    public partial class ChatController : ControllerBase<ChatView>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public ChatController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {
            TMP_InputField inputField;
            //inputField.preferredHeight use this for the resize of the view
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
