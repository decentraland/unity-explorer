using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace MVC
{
    public interface IController
    {
        ControllerState State { get; }

        CanvasOrdering.SortingLayer Layer { get; }

        /// <summary>
        ///     View is focused when the obscuring view disappears
        /// </summary>
        void Focus();

        /// <summary>
        ///     View is blurred when gets obscured by another view in the same stack
        /// </summary>
        void Blur();

        UniTask HideViewAsync(CancellationToken ct);

        void SetViewCanvasActive(bool isActive);

        void Dispose();
    }

    // ReSharper disable once UnusedTypeParameter TView it's used for registering a proper association in MVC Manager
    public interface IController<TView, in TInputData> : IController where TView: IView
    {
        /// <summary>
        ///     Shows the views and keeps spinning until the close intention is sent (e.g. by button)
        /// </summary>
        UniTask LaunchViewLifeCycleAsync(CanvasOrdering ordering, TInputData data, CancellationToken ct);
    }
}
