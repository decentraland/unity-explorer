using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using ECS.StreamableLoading.AssetBundles;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace DCL.UI.DebugMenu
{
    /// <summary>
    ///     Live view over <see cref="AbgenConversionMetrics" />: one row per in-process abgen conversion of the
    ///     current local-scene session, newest first, plus a summary line in the header.
    /// </summary>
    public class AbConversionPanelView : DebugPanelView
    {
        private const string USS_ENTRY = "ab-conversion-entry";
        private const string USS_ENTRY_WARNING = "ab-conversion-entry--warning";
        private const string USS_ENTRY_ERROR = "ab-conversion-entry--error";

        private readonly List<AbgenConversionMetrics.Entry> rows = new ();
        private readonly ListView list;
        private readonly Label summary;
        private readonly Button clearCacheButton;
        private int lastVersion = -1;

        public AbConversionPanelView(VisualElement root, Button sidebarButton, Action closeClicked) : base(root, sidebarButton, closeClicked)
        {
            summary = root.Q<Label>("AbConversionSummary");
            clearCacheButton = root.Q<Button>("AbConversionClearCacheButton");
            clearCacheButton.clicked += OnClearCacheClicked;
            list = root.Q<ListView>("AbConversionList");
            list.makeItem = MakeRow;
            list.bindItem = BindRow;
            list.itemsSource = rows;
        }

        public void Refresh()
        {
            if (!Visible) return;

            AbgenConversionMetrics metrics = AbgenConversionMetrics.INSTANCE;
            if (metrics.Version == lastVersion) return;

            lastVersion = metrics.Version;

            metrics.CopySnapshot(rows);
            list.RefreshItems();

            string warmUp = metrics.WarmUp switch
                            {
                                AbgenConversionMetrics.WarmUpStage.Converting => $"<color=#FFC95B>SCENE CONVERTING</color> {metrics.WarmUpSceneId} · ",
                                AbgenConversionMetrics.WarmUpStage.Ready => metrics.WarmUpAlreadyWarm
                                    ? "<color=#63D471>SCENE ALREADY CONVERTED</color> (warm cache) · "
                                    : $"<color=#63D471>SCENE READY</color> in {metrics.WarmUpElapsedSeconds:F0}s · ",
                                AbgenConversionMetrics.WarmUpStage.Failed => "<color=#FF6C6C>SCENE CONVERSION FAILED</color> (lazy per-file conversion still active) · ",
                                _ => string.Empty,
                            };

            summary.text = $"{warmUp}{metrics.Planned} planned · {metrics.InFlight} converting · <color=#63D471>{metrics.Succeeded} ok</color> · <color=#FF6C6C>{metrics.Failed} failed</color> · {metrics.TotalOutputBytes / (1024 * 1024f):F1} MB";
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
