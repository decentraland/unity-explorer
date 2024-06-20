using Cysharp.Threading.Tasks;
using DCL.Input;
using JetBrains.Annotations;
using MVC;
using System.Threading;

namespace DCL.Passport
{
    public partial class PassportController : ControllerBase<PassportView, PassportController.Params>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private readonly ICursor cursor;

        private string currentUserId;

        public PassportController([NotNull] ViewFactoryMethod viewFactory, ICursor cursor) : base(viewFactory)
        {
            this.cursor = cursor;
        }

        protected override void OnViewShow()
        {
            currentUserId = inputData.UserId;
            cursor.Unlock();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(
                viewInstance.CloseButton.OnClickAsync(ct),
                viewInstance.BackgroundButton.OnClickAsync(ct));
    }
}
