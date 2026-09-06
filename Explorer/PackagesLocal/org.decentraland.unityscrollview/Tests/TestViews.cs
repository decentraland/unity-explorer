using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SuperScrollView.Tests
{
    /// <summary>
    /// Builds view hierarchies for the EditMode suite. Nothing here runs the
    /// player loop: a view initializes on its first public call, scrolling is
    /// driven through the <see cref="ScrollRect"/> event the view listens to, and
    /// per-frame work goes through the view's <c>Tick</c>.
    /// </summary>
    internal sealed class TestViews : IDisposable
    {
        public const string TEMPLATE_NAME = "Row";

        private readonly List<GameObject> roots = new ();

        public GameObject ListTemplate(float width, float height) =>
            Template(width, height, typeof(LoopListViewItem2));

        public GameObject GridTemplate(float width, float height) =>
            Template(width, height, typeof(LoopGridViewItem));

        public LoopListView2 List(ListItemArrangeType arrange, Vector2 viewportSize, GameObject template,
            int initCreateCount = 0, float padding = 0f, float startPosOffset = 0f,
            bool snap = false, Vector2 viewportSnapPivot = default, Vector2 itemSnapPivot = default)
        {
            var view = ViewObject(viewportSize).AddComponent<LoopListView2>();
            var so = new SerializedObject(view);
            so.FindProperty("mArrangeType").intValue = (int)arrange;
            so.FindProperty("mItemSnapEnable").boolValue = snap;
            so.FindProperty("mViewPortSnapPivot").vector2Value = viewportSnapPivot;
            so.FindProperty("mItemSnapPivot").vector2Value = itemSnapPivot;

            SerializedProperty entry = SinglePrefabEntry(so, template);
            entry.FindPropertyRelative("mPadding").floatValue = padding;
            entry.FindPropertyRelative("mInitCreateCount").intValue = initCreateCount;
            entry.FindPropertyRelative("mStartPosOffset").floatValue = startPosOffset;

            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        public LoopGridView Grid(ListItemArrangeType arrange, Vector2 viewportSize, GameObject template,
            int fixedCount, Vector2 itemSize, Vector2 itemPadding = default, Vector2 recycleDistance = default,
            int gridFixedType = 0, int padLeft = 0, int padTop = 0, int initCreateCount = 0,
            bool snap = false, Vector2 viewportSnapPivot = default, Vector2 itemSnapPivot = default)
        {
            var view = ViewObject(viewportSize).AddComponent<LoopGridView>();
            var so = new SerializedObject(view);
            so.FindProperty("mArrangeType").intValue = (int)arrange;
            so.FindProperty("mFixedRowOrColumnCount").intValue = fixedCount;
            so.FindProperty("mItemSize").vector2Value = itemSize;
            so.FindProperty("mItemPadding").vector2Value = itemPadding;
            so.FindProperty("mItemRecycleDistance").vector2Value = recycleDistance;
            so.FindProperty("mGridFixedType").intValue = gridFixedType;
            so.FindProperty("mPadding.m_Left").intValue = padLeft;
            so.FindProperty("mPadding.m_Top").intValue = padTop;
            so.FindProperty("mItemSnapEnable").boolValue = snap;
            so.FindProperty("mViewPortSnapPivot").vector2Value = viewportSnapPivot;
            so.FindProperty("mItemSnapPivot").vector2Value = itemSnapPivot;

            SerializedProperty entry = SinglePrefabEntry(so, template);
            entry.FindPropertyRelative("mInitCreateCount").intValue = initCreateCount;

            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        public void Dispose()
        {
            for (int i = 0; i < roots.Count; i++)
            {
                if (roots[i] != null)
                    UnityEngine.Object.DestroyImmediate(roots[i]);
            }

            roots.Clear();
        }

        public static void ScrollTo(LoopListView2 view, float leading) =>
            ScrollTo(view.ArrangeType, view.ScrollRect, leading);

        public static void ScrollTo(LoopGridView view, float leading) =>
            ScrollTo(view.ArrangeType, view.ScrollRect, leading);

        public static float Leading(LoopListView2 view) =>
            ScrollSpace.LeadingOfContent(view.ArrangeType, view.ScrollRect.content, view.ScrollRect.viewport.rect);

        public static float Leading(LoopGridView view) =>
            ScrollSpace.LeadingOfContent(view.ArrangeType, view.ScrollRect.content, view.ScrollRect.viewport.rect);

        /// <summary>
        /// Distance from the viewport's leading edge to the item's leading edge along
        /// the arrange axis: 0 when the item is flush, growing towards the trailing
        /// side.
        /// </summary>
        public static float LeadingEdgeInViewport(LoopListView2 view, Component item) =>
            EdgeInViewport(view.ArrangeType, view.ScrollRect, (RectTransform)item.transform);

        public static float LeadingEdgeInViewport(LoopGridView view, Component item) =>
            EdgeInViewport(view.ArrangeType, view.ScrollRect, (RectTransform)item.transform);

        /// <summary>Distance from the Content's min edge on the minor axis to the item's min edge.</summary>
        public static float MinorEdgeInContent(LoopGridView view, Component item)
        {
            RectTransform content = view.ScrollRect.content;
            Rect contentRect = content.rect;
            ScrollSpace.EdgesInParent((RectTransform)item.transform, contentRect, out Vector2 min, out Vector2 max);

            return ScrollSpace.IsVertical(view.ArrangeType) ? min.x - contentRect.xMin : contentRect.yMax - max.y;
        }

        public static int Instantiated<T>(Component view) where T: Component =>
            view.GetComponentsInChildren<T>(true).Length;

        public static int Active<T>(Component view) where T: Component
        {
            T[] all = view.GetComponentsInChildren<T>(true);
            var active = 0;

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].gameObject.activeSelf)
                    active++;
            }

            return active;
        }

        private GameObject Template(float width, float height, Type itemType)
        {
            var go = new GameObject(TEMPLATE_NAME, typeof(RectTransform), itemType);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            roots.Add(go);
            return go;
        }

        private GameObject ViewObject(Vector2 viewportSize)
        {
            var go = new GameObject("View", typeof(RectTransform));
            ((RectTransform)go.transform).sizeDelta = viewportSize;
            roots.Add(go);
            return go;
        }

        private static SerializedProperty SinglePrefabEntry(SerializedObject so, GameObject template)
        {
            SerializedProperty list = so.FindProperty("mItemPrefabDataList");
            list.arraySize = 1;
            SerializedProperty entry = list.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("mItemPrefab").objectReferenceValue = template;
            return entry;
        }

        private static void ScrollTo(ListItemArrangeType arrange, ScrollRect scrollRect, float leading)
        {
            ScrollSpace.SetLeadingOfContent(arrange, scrollRect.content, scrollRect.viewport.rect, leading);
            scrollRect.onValueChanged.Invoke(Vector2.zero);
        }

        private static float EdgeInViewport(ListItemArrangeType arrange, ScrollRect scrollRect, RectTransform rt)
        {
            RectTransform content = scrollRect.content;
            Rect contentRect = content.rect;
            ScrollSpace.EdgesInParent(rt, contentRect, out Vector2 min, out Vector2 max);

            float inContent = arrange switch
                              {
                                  ListItemArrangeType.TopToBottom => contentRect.yMax - max.y,
                                  ListItemArrangeType.BottomToTop => min.y - contentRect.yMin,
                                  ListItemArrangeType.LeftToRight => min.x - contentRect.xMin,
                                  ListItemArrangeType.RightToLeft => contentRect.xMax - max.x,
                                  _ => 0f,
                              };

            return inContent - ScrollSpace.LeadingOfContent(arrange, content, scrollRect.viewport.rect);
        }
    }

    /// <summary>
    /// Item provider for a list under test: rents the template row, sizes it per
    /// index along the arrange axis and counts how often each index was bound.
    /// </summary>
    internal sealed class RowProvider
    {
        public readonly Dictionary<int, int> Binds = new ();

        public Func<int, float> ExtentOf = static _ => 40f;

        private readonly bool vertical;

        public RowProvider(bool vertical)
        {
            this.vertical = vertical;
        }

        public LoopListViewItem2 Provide(LoopListView2 view, int index)
        {
            Binds[index] = (Binds.TryGetValue(index, out int bound) ? bound : 0) + 1;

            LoopListViewItem2 row = view.NewListViewItem(TestViews.TEMPLATE_NAME);
            var rt = (RectTransform)row.transform;
            Vector2 size = rt.sizeDelta;

            if (vertical)
                size.y = ExtentOf(index);
            else
                size.x = ExtentOf(index);

            rt.sizeDelta = size;
            return row;
        }
    }

    /// <summary>
    /// Item provider for a grid under test: rents the template cell and records the
    /// row/column each index was resolved to.
    /// </summary>
    internal sealed class CellProvider
    {
        public readonly Dictionary<int, int> Binds = new ();
        public readonly Dictionary<int, Vector2Int> Placement = new ();

        public LoopGridViewItem Provide(LoopGridView view, int index, int row, int column)
        {
            Binds[index] = (Binds.TryGetValue(index, out int bound) ? bound : 0) + 1;
            Placement[index] = new Vector2Int(row, column);
            return view.NewListViewItem(TestViews.TEMPLATE_NAME);
        }
    }
}
