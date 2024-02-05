using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;

namespace DCL.Chat
{
    public partial class ChatController : ControllerBase<ChatView>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public ChatController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {

        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);
    }
}
