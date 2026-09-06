using System;
using UnityEngine;

namespace SuperScrollView
{
    /// <summary>
    /// One row in a view's inspector-authored prefab table. A view registers a
    /// prefab under its GameObject name; item-provider callbacks then look the
    /// entry up (via <c>ItemPrefabDataList</c>) and pass
    /// <c>mItemPrefab.name</c> to <c>NewListViewItem</c> to spawn a pooled row.
    ///
    /// The field names mirror the serialized layout the prefabs were authored
    /// against, so existing prefab YAML binds without re-authoring.
    /// </summary>
    [Serializable]
    public class ItemPrefabConfData
    {
        /// <summary>Template the view instantiates for this kind of item.</summary>
        public GameObject mItemPrefab;

        /// <summary>Extra spacing appended after the item along the arrange axis.</summary>
        public float mPadding;

        /// <summary>Rows instantiated into the pool up front. The pool also grows on demand.</summary>
        public int mInitCreateCount;

        /// <summary>
        /// Inset before the first item's leading edge. A list reads it from its first
        /// prefab entry, since the inset belongs to the list rather than to a kind of
        /// row; grids take their leading inset from <c>mPadding</c> instead.
        /// </summary>
        public float mStartPosOffset;

        /// <summary>Optional size hint (major axis) used to seed layout before an item is measured.</summary>
        public float mItemSize;
    }
}
