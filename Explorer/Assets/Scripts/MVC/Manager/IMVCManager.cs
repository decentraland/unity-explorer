using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace MVC
{
    /// <summary>
    ///     An entry point to show the view
    /// </summary>
    public interface IMVCManager : IDisposable
    {
        event Action<IController> OnViewShowed;
        event Action<IController> OnViewClosed;

        /// <summary>
        ///     Called externally to schedule a view opening
        /// </summary>
        /// <param name="command"></param>
        /// <param name="ct">Additional cancellation token from the caller side</param>
        /// <typeparam name="TView"></typeparam>
        /// <typeparam name="TInputData"></typeparam>
        UniTask ShowAsync<TView, TInputData>(ShowCommand<TView, TInputData> command, CancellationToken ct = default) where TView: IView;

        void RegisterController<TView, TInputData>(IController<TView, TInputData> controller) where TView: IView;

        void SetAllViewsCanvasActive(bool isActive);
    }
}
