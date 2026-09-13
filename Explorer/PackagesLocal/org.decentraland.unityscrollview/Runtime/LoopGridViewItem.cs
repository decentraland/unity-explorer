using UnityEngine;

namespace SuperScrollView
{
    /// <summary>
    /// Component present on every cell prefab a <see cref="LoopGridView"/>
    /// recycles. The view stamps the cell's data index and its resolved
    /// row/column each time it is bound, so provider callbacks can position
    /// their content within the grid.
    /// </summary>
    public class LoopGridViewItem : MonoBehaviour
    {
        /// <summary>Data index this cell is currently bound to, or -1 while pooled.</summary>
        public int ItemIndex { get; set; } = -1;

        /// <summary>Grid row this cell occupies.</summary>
        public int RowIndex { get; set; } = -1;

        /// <summary>Grid column this cell occupies.</summary>
        public int ColumnIndex { get; set; } = -1;

        /// <summary>Name of the prefab this cell was spawned from; keys its recycle pool.</summary>
        public string ItemPrefabName { get; internal set; } = string.Empty;
    }
}
