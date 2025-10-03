using Cysharp.Threading.Tasks;
using DCL.Utility;
using MVC;
using System.Threading;

namespace DCL.UI.ErrorPopup
{
    public class ErrorPopupWithRetryController : ControllerBase<ErrorPopupWithRetryView, ErrorPopupWithRetryController.Input>
    {
        public ErrorPopupWithRetryController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            viewInstance!.ExitButton.onClick.AddListener(() =>
            {
                ExitUtils.Exit();
                inputData.SelectedOption = Result.EXIT;
            });

            viewInstance.RestartButton.onClick.AddListener(() => inputData.SelectedOption = Result.RESTART);
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();

            viewInstance!.DescriptionText.text = inputData.Description;
            viewInstance!.TitleText.text = inputData.Title;
            viewInstance.InternetLostIcon.SetActive(inputData.IconType == IconType.CONNECTION_LOST);
            viewInstance.ErrorIcon.SetActive(inputData.IconType == IconType.ERROR);
            viewInstance.WarningIcon.SetActive(inputData.IconType == IconType.WARNING);
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance!.ExitButton.OnClickAsync(ct), viewInstance.RestartButton.OnClickAsync(ct));

        public enum Result
        {
            EXIT,
            RESTART,
        }

        public enum IconType
        {
            WARNING,
            ERROR,
            CONNECTION_LOST,
        }

        public class Input
        {
            /// <summary>
            /// Out value
            /// </summary>
            public Result SelectedOption;

            public string Title = "Error";
            public string Description = "An error was encountered. Please reload to try again.";

            public IconType IconType = IconType.ERROR;
        }
    }
}
