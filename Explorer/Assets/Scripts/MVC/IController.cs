using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace MVC
{
    public interface IController
    {
        CanvasOrdering.SortingLayer SortLayers { get; }

        /// <summary>
        ///     View is focused when the obscuring view disappears
        /// </summary>
        void OnFocus();

        /// <summary>
        ///     View is blurred when gets obscured by another view in the same stack
        /// </summary>
        void OnBlur();

        /// <summary>
        ///     Should be called from <see cref="IMVCManager" /> only
        /// </summary>
        internal UniTask HideView(CancellationToken ct);
    }

    // ReSharper disable once UnusedTypeParameter TView it's used for registering a proper association in MVC Manager
    public interface IController<TView, in TInputData> : IController where TView: MonoBehaviour, IView
    {
        /// <summary>
        ///     Shows the views and keeps spinning until the close intention is sent (e.g. by button)
        /// </summary>
        UniTask LaunchViewLifeCycle(CanvasOrdering ordering, TInputData inputData, CancellationToken ct);
    }
}
