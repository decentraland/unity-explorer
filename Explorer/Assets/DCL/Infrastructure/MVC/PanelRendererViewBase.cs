using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace MVC
{
    /// <summary>
    ///     UI Toolkit counterpart of <see cref="ViewBase" />. The layer offset lives on the PanelSettings asset, so
    ///     <see cref="SetDrawOrder" /> only orders the renderers that share that panel.
    /// </summary>
    public abstract class PanelRendererViewBase : MonoBehaviour, IView
    {
        private UniTaskCompletionSource? rootLoaded;

        [field: SerializeField] protected PanelRenderer panelRenderer { get; private set; } = null!;

        // Null while the object is inactive; the renderer hands the hierarchy over through its reload callback
        protected VisualElement? root { get; private set; }

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
            root?.SetVisible(isActive);
        }

        protected virtual UniTask PlayShowAnimationAsync(CancellationToken ct) =>
            UniTask.CompletedTask;

        protected virtual UniTask PlayHideAnimationAsync(CancellationToken ct) =>
            UniTask.CompletedTask;

        // Invoked at once when the hierarchy is already attached, otherwise from the panel update that attaches it
        protected virtual void OnEnable()
        {
            panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }

        protected virtual void OnDisable()
        {
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);
            root = null;
        }

        private void OnUIReload(PanelRenderer renderer, VisualElement rootElement)
        {
            root = rootElement;
            rootLoaded?.TrySetResult();
            rootLoaded = null;
        }

        // Resumes later in the frame: the reload callback runs while the runtime iterates its panel renderers
        private async UniTask WaitForRootAsync(CancellationToken ct)
        {
            if (root != null)
                return;

            rootLoaded ??= new UniTaskCompletionSource();
            await rootLoaded.Task.AttachExternalCancellation(ct);
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);
        }
    }
}
