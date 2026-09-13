using UnityEngine;

namespace SuperScrollView
{
    /// <summary>
    /// Component present on every row prefab a <see cref="LoopListView2"/>
    /// recycles. The view stamps <see cref="ItemIndex"/> and
    /// <see cref="ItemPrefabName"/> each time the row is bound to a data index;
    /// consumers read those back and set <see cref="Padding"/> to reserve extra
    /// trailing space (e.g. a chat date divider) for the row.
    /// </summary>
    public class LoopListViewItem2 : MonoBehaviour
    {
        /// <summary>Data index this row is currently bound to, or -1 while pooled.</summary>
        public int ItemIndex { get; set; } = -1;

        /// <summary>Name of the prefab this row was spawned from; keys its recycle pool.</summary>
        public string ItemPrefabName { get; internal set; } = string.Empty;

        /// <summary>Extra spacing reserved after this row along the arrange axis.</summary>
        public float Padding { get; set; }

        /// <summary>
        /// Major-axis extent the owning view measured for this row at bind time.
        /// Zero while the row has never been laid out, in which case
        /// <see cref="ItemSizeWithPadding"/> falls back to the live rect.
        /// </summary>
        internal float MeasuredSize { get; set; }

        /// <summary>
        /// Whether the owning view arranges along Y. Stamped at bind time so the
        /// fallback in <see cref="ItemSizeWithPadding"/> reads the same axis the
        /// view measures, rather than assuming a vertical list.
        /// </summary>
        internal bool ArrangeVertical { get; set; } = true;

        /// <summary>
        /// The row's laid-out extent along the arrange axis plus its
        /// <see cref="Padding"/>. Consumers use this to keep visible rows
        /// stationary while inserting new data.
        /// </summary>
        public float ItemSizeWithPadding
        {
            get
            {
                float extent = MeasuredSize;

                if (extent <= 0f)
                {
                    var rt = transform as RectTransform;

                    if (rt != null)
                    {
                        Rect r = rt.rect;
                        extent = ArrangeVertical ? r.height : r.width;
                    }
                }

                return extent + Padding;
            }
        }
    }
}
