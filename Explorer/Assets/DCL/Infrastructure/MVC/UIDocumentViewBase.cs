using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace MVC
{
    /// <summary>
    ///     UI Toolkit counterpart of <see cref="ViewBase" />: a view drawn by a <see cref="UIDocument" /> instead of a Canvas.
    ///     A panel sorts against uGUI canvases as a whole, so the layer offset of <see cref="CanvasOrdering" /> is authored on the
    ///     document's PanelSettings asset (one asset per <see cref="CanvasOrdering.SortingLayer" />); <see cref="SetDrawOrder" />
    ///     only orders documents that share that panel.
    /// </summary>
    public abstract partial class UIDocumentViewBase : MonoBehaviour, IView
    {
        // Implemented only under ALTTESTER (UIDocumentViewBase.AltTester.cs); erased with its call sites otherwise.
        partial void ReportViewState(string state);

        [field: SerializeField] protected UIDocument document { get; private set; } = null!;

        /// <summary>
        ///     Exists only while the object is active: the document builds its hierarchy in OnEnable and drops it in OnDisable.
        /// </summary>
        protected VisualElement? Root => document.rootVisualElement;

        public void SetDrawOrder(CanvasOrdering order)
        {
            document.sortingOrder = order.OrderInLayer;
        }

        public virtual async UniTask ShowAsync(CancellationToken ct)
        {
            ReportViewState("Showing");
            gameObject.SetActive(true);
            await PlayShowAnimationAsync(ct);
            ReportViewState("Shown");
        }

        public virtual async UniTask HideAsync(CancellationToken ct, bool isInstant = false)
        {
            ReportViewState("Hiding");

            if (!isInstant)
                await PlayHideAnimationAsync(ct);

            gameObject.SetActive(false);
            ReportViewState("Hidden");
        }

        public virtual void SetCanvasActive(bool isActive)
        {
            Root?.SetVisible(isActive);
        }

        protected virtual UniTask PlayShowAnimationAsync(CancellationToken ct) =>
            UniTask.CompletedTask;

        protected virtual UniTask PlayHideAnimationAsync(CancellationToken ct) =>
            UniTask.CompletedTask;
    }
}
