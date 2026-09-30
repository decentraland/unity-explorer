using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace MVC
{
    /// <summary>
    ///     UI Toolkit counterpart of <see cref="ViewBase" />: a view drawn by a <see cref="PanelRenderer" /> instead of a Canvas.
    ///     A panel sorts against uGUI canvases as a whole, so the layer offset of <see cref="CanvasOrdering" /> is authored on the
    ///     renderer's PanelSettings asset (one asset per <see cref="CanvasOrdering.SortingLayer" />); <see cref="SetDrawOrder" />
    ///     only orders renderers that share that panel.
    /// </summary>
    public abstract class PanelRendererViewBase : MonoBehaviour, IView
    {
        private UniTaskCompletionSource? rootLoaded;

        [field: SerializeField] protected PanelRenderer panelRenderer { get; private set; } = null!;

        /// <summary>
        ///     Exposed only while the object is active. The renderer keeps the hierarchy across hides, detached from its panel, and
        ///     hands it over through its reload callback once it is attached again; <see cref="ShowAsync" /> waits for that.
        /// </summary>
        protected VisualElement? Root { get; private set; }

        public void SetDrawOrder(CanvasOrdering order)
        {
            panelRenderer.sortingOrder = order.OrderInLayer;
        }

        public virtual async UniTask ShowAsync(CancellationToken ct)
        {
            gameObject.SetActive(true);
            await WaitForRootAsync(ct);
            await PlayShowAnimationAsync(ct);
        }

        public virtual async UniTask HideAsync(CancellationToken ct, bool isInstant = false)
        {
            if (!isInstant)
                await PlayHideAnimationAsync(ct);

            gameObject.SetActive(false);
        }

        public virtual void SetCanvasActive(bool isActive)
        {
            Root?.SetVisible(isActive);
        }

        protected virtual UniTask PlayShowAnimationAsync(CancellationToken ct) =>
            UniTask.CompletedTask;

        protected virtual UniTask PlayHideAnimationAsync(CancellationToken ct) =>
            UniTask.CompletedTask;

        // Invoked at once when the hierarchy is already attached to its panel, otherwise from the panel update that attaches it
        protected virtual void OnEnable()
        {
            panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }

        protected virtual void OnDisable()
        {
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);
            Root = null;
        }

        private void OnUIReload(PanelRenderer renderer, VisualElement rootElement)
        {
            Root = rootElement;
            rootLoaded?.TrySetResult();
            rootLoaded = null;
        }

        // The callback runs while the runtime iterates its panel renderers, so the show flow resumes later in the frame:
        // continuing inline would mutate that collection as soon as the flow enables or disables another panel
        private async UniTask WaitForRootAsync(CancellationToken ct)
        {
            if (Root != null)
                return;

            rootLoaded ??= new UniTaskCompletionSource();
            await rootLoaded.Task.AttachExternalCancellation(ct);
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);
        }
    }
}
