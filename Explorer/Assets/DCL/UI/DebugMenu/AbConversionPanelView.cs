using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using ECS.StreamableLoading.AssetBundles;
using ECS.Unity.GLTFContainer.Asset.Components;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.UI.DebugMenu
{
    /// <summary>
    ///     Live view over <see cref="AbgenConversionMetrics" />: one row per abgen conversion of the
    ///     current local-scene session in chronological order (newest at the bottom, following the
    ///     scroll like the debug console), plus a summary line in the header.
    /// </summary>
    public class AbConversionPanelView : DebugPanelView
    {
        private const string USS_ENTRY = "ab-conversion-entry";
        private const string USS_ENTRY_WARNING = "ab-conversion-entry--warning";
        private const string USS_ENTRY_ERROR = "ab-conversion-entry--error";

        private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
        private static readonly int COLOR_ID = Shader.PropertyToID("_Color");

        private readonly List<AbgenConversionMetrics.Entry> rows = new ();
        private readonly ListView list;
        private readonly ScrollView scrollView;
        private readonly Label summary;
        private readonly Button clearCacheButton;
        private readonly Button highlightButton;
        private readonly List<Renderer> highlightedRenderers = new ();
        private readonly List<Renderer> renderersScratch = new ();
        private int lastVersion = -1;
        private bool highlightActive;

        public AbConversionPanelView(VisualElement root, Button sidebarButton, Action closeClicked) : base(root, sidebarButton, closeClicked)
        {
            summary = root.Q<Label>("AbConversionSummary");
            clearCacheButton = root.Q<Button>("AbConversionClearCacheButton");
            clearCacheButton.clicked += OnClearCacheClicked;
            highlightButton = root.Q<Button>("AbConversionHighlightButton");
            highlightButton.clicked += OnHighlightClicked;
            list = root.Q<ListView>("AbConversionList");
            list.makeItem = MakeRow;
            list.bindItem = BindRow;
            list.itemsSource = rows;
            scrollView = list.Q<ScrollView>();
        }

        public override void Toggle()
        {
            base.Toggle();

            // Land on the newest rows when opened; -1 forces the rebuild even if nothing changed since.
            if (Visible)
            {
                lastVersion = -1;
                Refresh(scrollToBottom: true);
            }
        }

        public void Refresh(bool scrollToBottom = false)
        {
            if (!Visible) return;

            AbgenConversionMetrics metrics = AbgenConversionMetrics.INSTANCE;
            if (metrics.Version == lastVersion) return;

            lastVersion = metrics.Version;

            // Follow the tail only while the user is already at it, mirroring ConsolePanelView.
            bool atBottom = scrollToBottom || scrollView == null || scrollView.verticalScroller.value >= scrollView.verticalScroller.highValue * 0.999f;

            metrics.CopySnapshot(rows);
            list.RefreshItems();

            if (atBottom && rows.Count > 0)
                list.ScrollToItem(rows.Count - 1);

            string warmUp = metrics.WarmUp switch
                            {
                                AbgenConversionMetrics.WarmUpStage.Converting => $"<color=#FFC95B>SCENE CONVERTING</color> {metrics.WarmUpSceneId} · ",
                                AbgenConversionMetrics.WarmUpStage.Ready => metrics.WarmUpAlreadyWarm
                                    ? "<color=#63D471>SCENE ALREADY CONVERTED</color> (warm cache) · "
                                    : $"<color=#63D471>SCENE READY</color> in {metrics.WarmUpElapsedSeconds:F0}s · ",
                                AbgenConversionMetrics.WarmUpStage.Failed => "<color=#FF6C6C>SCENE CONVERSION FAILED</color> (lazy per-file conversion still active) · ",
                                _ => string.Empty,
                            };

            // The server's own done/total counter is the truth for a whole-scene build; the sampled
            // row-derived counters only describe sessions without a warm-up (lazy conversions).
            string counts = metrics.WarmUpTotal > 0
                ? $"<color=#63D471>{metrics.WarmUpDone}/{metrics.WarmUpTotal}</color> converted"
                : $"{metrics.Planned} planned · {metrics.InFlight} converting · <color=#63D471>{metrics.Succeeded} ok</color>";

            summary.text = $"{warmUp}{counts} · <color=#FF6C6C>{metrics.Failed} failed</color> · {metrics.TotalOutputBytes / (1024 * 1024f):F1} MB";
        }

        /// <summary>
        ///     Tints every scene object green when it was loaded from an asset bundle and red when it fell
        ///     back to a raw GLTF, keyed off the source-prefixed root names Utils stamps at creation.
        ///     Property blocks only — shared materials are never touched; a second click restores them.
        /// </summary>
        private void OnHighlightClicked()
        {
            highlightActive = !highlightActive;
            highlightButton.text = highlightActive ? "CLEAR HIGHLIGHT" : "HIGHLIGHT SOURCES";

            if (!highlightActive)
            {
                foreach (Renderer renderer in highlightedRenderers)
                    if (renderer)
                        renderer.SetPropertyBlock(null);

                highlightedRenderers.Clear();
                return;
            }

            var abTint = new MaterialPropertyBlock();
            abTint.SetColor(BASE_COLOR_ID, Color.green);
            abTint.SetColor(COLOR_ID, Color.green);

            var gltfTint = new MaterialPropertyBlock();
            gltfTint.SetColor(BASE_COLOR_ID, Color.red);
            gltfTint.SetColor(COLOR_ID, Color.red);

            var abRoots = 0;
            var gltfRoots = 0;

            Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);

            foreach (Transform sceneTransform in transforms)
            {
                bool fromAssetBundle = sceneTransform.name.StartsWith(GltfContainerAsset.AB_ROOT_NAME_PREFIX, StringComparison.Ordinal);

                if (!fromAssetBundle && !sceneTransform.name.StartsWith(GltfContainerAsset.RAW_GLTF_ROOT_NAME_PREFIX, StringComparison.Ordinal))
                    continue;

                if (fromAssetBundle) abRoots++;
                else gltfRoots++;

                sceneTransform.GetComponentsInChildren(renderersScratch);

                foreach (Renderer renderer in renderersScratch)
                {
                    renderer.SetPropertyBlock(fromAssetBundle ? abTint : gltfTint);
                    highlightedRenderers.Add(renderer);
                }
            }

            AbgenConversionMetrics.INSTANCE.OnMilestone($"highlight — {abRoots} objects from asset bundles (green) · {gltfRoots} from raw GLTF (red)");
        }

        private void OnClearCacheClicked() =>
            ClearCacheAsync().Forget();

        private async UniTaskVoid ClearCacheAsync()
        {
            clearCacheButton.SetEnabled(false);

            try
            {
                // Reads persistentDataPath, so it must run on the main thread; only the walk goes to the pool.
                string[] roots = AbgenBundleDiskCache.AllBundleRoots();

                AbgenBundleDiskCache.ClearResult result = await UniTask.RunOnThreadPool(() => AbgenBundleDiskCache.ClearAll(roots));

                string skipped = result.SkippedFiles > 0 ? $" · <color=#FFC95B>{result.SkippedFiles} in use, skipped</color>" : string.Empty;
                summary.text = $"cache cleared: {result.DeletedFiles} bundles · {result.DeletedBytes / (1024 * 1024f):F1} MB freed{skipped}";
            }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.ASSET_BUNDLES); }
            finally { clearCacheButton.SetEnabled(true); }
        }

        private static VisualElement MakeRow()
        {
            var label = new Label();
            label.AddToClassList(USS_ENTRY);
            return label;
        }

        private void BindRow(VisualElement element, int index)
        {
            if (index < 0 || index >= rows.Count) return;

            AbgenConversionMetrics.Entry entry = rows[index];
            var label = (Label)element;

            label.text = entry.Status switch
                         {
                             AbgenConversionMetrics.ConversionStatus.Converting => $"CONVERTING  {entry.Path}",
                             AbgenConversionMetrics.ConversionStatus.Converted => $"OK  {entry.Path}  —  {entry.OutputBytes / 1024} KB · {entry.ElapsedMs} ms · {entry.ArtifactName}",
                             AbgenConversionMetrics.ConversionStatus.Processed => $"DONE  {entry.Path}",
                             AbgenConversionMetrics.ConversionStatus.Failed => $"FAILED  {entry.Path}  —  {entry.Error}",
                             AbgenConversionMetrics.ConversionStatus.Cancelled => $"CANCELLED  {entry.Path}",
                             AbgenConversionMetrics.ConversionStatus.Milestone => $"●  {entry.Path}",
                             _ => entry.Path,
                         };

            label.EnableInClassList(USS_ENTRY_WARNING, entry.Status == AbgenConversionMetrics.ConversionStatus.Converting);
            label.EnableInClassList(USS_ENTRY_ERROR, entry.Status == AbgenConversionMetrics.ConversionStatus.Failed);
        }
    }
}
