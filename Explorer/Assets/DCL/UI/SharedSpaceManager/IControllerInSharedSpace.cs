using Cysharp.Threading.Tasks;
using MVC;
using System;
using System.Threading;

namespace DCL.UI.SharedSpaceManager
{
    /// <summary>
    ///     <inheritdoc cref="IControllerInSharedSpace{TView,TInputData}" />
    /// </summary>
    /// <typeparam name="TView"></typeparam>
    public interface IControllerInSharedSpace<TView> : IControllerInSharedSpace<TView, ControllerNoData> where TView: IView { }

    /// <summary>
    ///     A controller that is managed by the Shared Space Manager.
    ///     Can contain additional logic that is common for all controllers and introduces generic parameters to maintain type-safety
    /// </summary>
    public interface IControllerInSharedSpace<TView, in TInputData> : IController<TView, TInputData>, IPanelInSharedSpace<TInputData> where TView: IView
    {
        /// <summary>
        ///     <inheritdoc cref="IPanelInSharedSpace{T}.IsVisibleInSharedSpace" />
        /// </summary>
        bool IPanelInSharedSpace.IsVisibleInSharedSpace => State != ControllerState.ViewHidden;

        /// <summary>
        ///     <inheritdoc cref="IPanelInSharedSpace{T}.OnShownInSharedSpaceAsync" />
        /// </summary>
        UniTask IPanelInSharedSpace<TInputData>.OnShownInSharedSpaceAsync(CancellationToken ct, TInputData parameters)
        {
            // Implementation for non-persistent controllers is not required
            if (Layer != CanvasOrdering.SortingLayer.Persistent)
                return UniTask.CompletedTask;

            throw new NotImplementedException($"{nameof(OnShownInSharedSpaceAsync)} must be implemented by the panel.");
        }
    }
}
