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
        private readonly List<AbgenConversionMetrics.Entry> rows = new ();
        private readonly ListView list;
        private readonly Label summary;
        private int lastVersion = -1;

        public AbConversionPanelView(VisualElement root, Button sidebarButton, Action closeClicked) : base(root, sidebarButton, closeClicked)
        {
            summary = root.Q<Label>("AbConversionSummary");
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

            summary.text = $"{metrics.Planned} planned · {metrics.InFlight} converting · <color=#63D471>{metrics.Succeeded} ok</color> · <color=#FF6C6C>{metrics.Failed} failed</color> · {metrics.TotalOutputBytes / (1024 * 1024f):F1} MB";
        }

        private static VisualElement MakeRow()
        {
            var label = new Label();
            label.style.paddingLeft = 8;
            label.style.paddingTop = 2;
            label.style.paddingBottom = 2;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        private void BindRow(VisualElement element, int index)
        {
            if (index < 0 || index >= rows.Count) return;

            AbgenConversionMetrics.Entry entry = rows[index];

            ((Label)element).text = entry.Status switch
                                    {
                                        AbgenConversionMetrics.ConversionStatus.Converting => $"<color=#FFC95B>CONVERTING</color>  {entry.Path}",
                                        AbgenConversionMetrics.ConversionStatus.Converted => $"<color=#63D471>OK</color>  {entry.Path}  —  {entry.OutputBytes / 1024} KB · {entry.ElapsedMs} ms · {entry.ArtifactName}",
                                        AbgenConversionMetrics.ConversionStatus.Failed => $"<color=#FF6C6C>FAILED</color>  {entry.Path}  —  {entry.Error}",
                                        AbgenConversionMetrics.ConversionStatus.Cancelled => $"<color=#8E8E8E>CANCELLED</color>  {entry.Path}",
                                        _ => entry.Path,
                                    };
        }
    }
}
