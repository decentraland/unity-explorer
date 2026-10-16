using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.TextCore.Text;
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
            BuildSpriteLookupTables(panelRenderer.panelSettings);
            panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }

        protected virtual void OnDisable()
        {
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);
            root = null;
        }

        /// <summary>
        ///     Builds the sprite character tables of the panel's emoji sprite assets on the main thread, before the first
        ///     repaint. The text jobs otherwise build them lazily from several worker threads at once, which corrupts the
        ///     dictionaries and leaves every label with a character outside the fonts blank from then on (Unity 6000.5.9,
        ///     SpriteAsset.UpdateLookupTables). A table already built is only read by the jobs.
        /// </summary>
        private static void BuildSpriteLookupTables(PanelSettings panelSettings)
        {
            PanelTextSettings textSettings = panelSettings.textSettings;
            var built = new HashSet<SpriteAsset>();
            BuildSpriteLookupTable(textSettings.defaultSpriteAsset, built);

            foreach (UnityEngine.TextCore.Text.TextAsset fallback in textSettings.emojiFallbackTextAssets)
                if (fallback is SpriteAsset spriteAsset)
                    BuildSpriteLookupTable(spriteAsset, built);
        }

        // The sprite assets are unassigned in a fresh text settings asset; the visited set stops a fallback cycle
        private static void BuildSpriteLookupTable(SpriteAsset? spriteAsset, HashSet<SpriteAsset> built)
        {
            if (spriteAsset == null || !built.Add(spriteAsset)) return;

            _ = spriteAsset.spriteCharacterLookupTable;

            foreach (SpriteAsset fallback in spriteAsset.fallbackSpriteAssets)
                BuildSpriteLookupTable(fallback, built);
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
