using Cysharp.Threading.Tasks;
using DCL.Utility;
using MVC;
using System.Threading;

namespace DCL.ApplicationsGuards.ApplicationLoadErrorGuard
{
    public class ApplicationLoadErrorGuardController : ControllerBase<ApplicationLoadErrorGuardView>
    {
        public Result SelectedOption { get; private set; }

        public ApplicationLoadErrorGuardController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance!.ExitButton.onClick.AddListener(() =>
            {
                ExitUtils.Exit();
                SelectedOption = Result.EXIT;
            });

            viewInstance.RestartButton.onClick.AddListener(() => SelectedOption = Result.RESTART);
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance!.ExitButton.OnClickAsync(ct), viewInstance.RestartButton.OnClickAsync(ct));

        public enum Result
        {
            EXIT,
            RESTART,
        }
    }
}
