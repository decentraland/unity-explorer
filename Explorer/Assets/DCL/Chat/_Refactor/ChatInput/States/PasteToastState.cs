using Cysharp.Threading.Tasks;
using DCL.UI;
using MVC;
using System.Threading;
using Utility;

namespace DCL.Chat.ChatInput
{
    public class PasteToastState : IndependentMVCState<ChatInputStateContext>
    {
        private readonly ChatInputView view;
        private readonly CancellationToken disposalCt;
        private CancellationTokenSource cts = new ();

        public PasteToastState(ChatInputView view, ChatInputStateContext context, CancellationToken disposalCt) : base(context)
        {
            this.view = view;
            this.disposalCt = disposalCt;
        }

        protected override void Activate(ControllerNoData input)
        {
            cts = new CancellationTokenSource();
            var data = new PastePopupToastData(view.pastePopupPosition.position, cts.Token.ToUniTask().Item1);
            ViewDependencies.GlobalUIViews.ShowPastePopupToastAsync(data, disposalCt).Forget();
        }

        protected override void Deactivate()
        {
            cts.SafeCancelAndDispose();
        }
    }
}
