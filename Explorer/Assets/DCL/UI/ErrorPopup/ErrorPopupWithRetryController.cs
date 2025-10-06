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

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

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
            viewInstance!.RetryButtonText.text = inputData.RetryText;
            viewInstance!.ExitButtonText.text = inputData.ExitText;
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

            public readonly string Title;
            public readonly string Description;
            public readonly string RetryText;
            public readonly string ExitText;
            public readonly IconType IconType;

            public Input(string title = "Error",
                string description = "An error was encountered. Please reload to try again.",
                string retryText = "Reload",
                string exitText = "Exit Application",
                IconType iconType = IconType.ERROR)
            {
                Title = title;
                Description = description;
                RetryText = retryText;
                ExitText = exitText;
                IconType = iconType;
            }
        }
    }
}
