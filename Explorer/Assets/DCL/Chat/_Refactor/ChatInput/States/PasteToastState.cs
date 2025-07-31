using Cysharp.Threading.Tasks;
using DCL.UI;
using MVC;
using System.Threading;
using Utility;

namespace DCL.Chat.ChatInput
{
    public class PasteToastState : IndependentMVCState<ChatInputStateContext>
    {
        private readonly CancellationToken disposalCt;
        private CancellationTokenSource cts = new ();

        public PasteToastState(ChatInputStateContext context, CancellationToken disposalCt) : base(context)
        {
            this.disposalCt = disposalCt;
        }

        protected override void Activate(ControllerNoData input)
        {
            cts = new CancellationTokenSource();
            var data = new PastePopupToastData(context.ChatInputView.pastePopupPosition.position, cts.Token.ToUniTask().Item1);
            ViewDependencies.GlobalUIViews.ShowPastePopupToastAsync(data, disposalCt).Forget();
        }

        protected override void Deactivate()
        {
            cts.SafeCancelAndDispose();
        }
    }
}
