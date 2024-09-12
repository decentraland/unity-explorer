using Cysharp.Threading.Tasks;
using System.Threading;

namespace MVC
{
    public static class ShowCommandExtensions
    {
        /// <summary>
        ///     All this hustle is to avoid boxing allocation on casting TInputData
        /// </summary>
        public static UniTask Execute<TView, TInputData>(
            this ref ShowCommand<TView, TInputData> command,
            IController controller, CanvasOrdering canvasOrdering, CancellationToken ct) where TView: IView
        {
            var castedController = (IController<TView, TInputData>)controller;
            return castedController.LaunchViewLifeCycleAsync(canvasOrdering, command.InputData, ct);
        }
    }
}
