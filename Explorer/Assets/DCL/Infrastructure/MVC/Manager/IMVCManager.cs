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

        void SetAllViewsCanvasActive(IController except, bool isActive);

        /// <summary>
        ///     Closes all POPUPS, FULLSCREEN and OVERLAY views currently open, leaving only PERSISTENT ones.
        /// </summary>
        void CloseAllNonPersistentViews(CancellationToken ct = default);

        /// <summary>
        ///     True while a POPUP or FULLSCREEN view is covering the persistent HUD.
        ///     Transient OVERLAY toasts (e.g. notifications) don't count.
        /// </summary>
        bool IsAnyModalViewShowing();

        /// <summary>
        ///     True while the controller registered for this view is anything but hidden — showing, focused,
        ///     blurred, or in the middle of hiding. This is the same condition under which
        ///     <see cref="ShowAsync{TView,TInputData}" /> returns without doing anything, so a caller can tell
        ///     in advance that its show command would be swallowed. False if no such controller is registered.
        /// </summary>
        bool IsShowing<TView, TInputData>() where TView: IView;
    }

    public static class ManagerExtensions
    {
        public static void ShowAndForget<TView, TInputData>(this IMVCManager mvcManager, ShowCommand<TView, TInputData> command, CancellationToken ct = default) where TView: IView
        {
            mvcManager.ShowAsync(command, ct).Forget();
        }
    }
}
