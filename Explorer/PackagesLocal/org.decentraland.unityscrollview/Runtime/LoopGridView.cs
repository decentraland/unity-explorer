using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SuperScrollView
{
    /// <summary>
    /// A recycling grid built on a uGUI <see cref="ScrollRect"/>. The grid has a
    /// fixed number of cells across the minor axis and grows along the major
    /// axis; cells are a uniform size. Only the lines (rows for a vertical grid,
    /// columns for a horizontal one) that overlap the viewport — widened by
    /// <c>mItemRecycleDistance</c> on each side — are materialized, and cells
    /// beyond that are pooled per prefab and rebound as the grid scrolls.
    ///
    /// The cells-across count comes from <c>mFixedRowOrColumnCount</c> when
    /// <c>mGridFixedType</c> is 0, and is derived from the viewport when it is 1.
    /// The cell size comes from <c>mItemSize</c>, falling back per component to
    /// the item prefab's own rect where <c>mItemSize</c> leaves it unset.
    ///
    /// Layout writes anchored positions and the Content's major-axis size, and
    /// nothing else: anchors, pivots and authored sizes on both the cells and the
    /// Content are left as the prefab set them. See <see cref="ScrollSpace"/> for
    /// the coordinate model.
    ///
    /// Serialized field names match the layout the prefabs were authored against
    /// so existing prefab YAML binds unchanged.
    /// </summary>
    [DisallowMultipleComponent]
    public class LoopGridView : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        [SerializeField] private List<ItemPrefabConfData> mItemPrefabDataList = new ();
        [SerializeField] private ListItemArrangeType mArrangeType = ListItemArrangeType.TopToBottom;
        [SerializeField] private int mFixedRowOrColumnCount = 1;
        [SerializeField] private RectOffset mPadding;
        [SerializeField] private Vector2 mItemPadding;
        [SerializeField] private Vector2 mItemSize = new (100f, 100f);
        [SerializeField] private Vector2 mItemRecycleDistance;
        [SerializeField] private bool mItemSnapEnable;
        [SerializeField] private int mGridFixedType;
        [SerializeField] private Vector2 mViewPortSnapPivot;
        [SerializeField] private Vector2 mItemSnapPivot;

        // Squared px/s below which the ScrollRect's inertia counts as spent. Snapping
        // waits for that instead of firing on end-of-drag, so a flick still flings.
        private const float SNAP_REST_SPEED_SQR = 400f;

        private ScrollRect scrollRect;
        private RectTransform content;
        private int count;
        private Func<LoopGridView, int, int, int, LoopGridViewItem> provideItem;
        private bool initialized;

        // Resolved once per pass so every index in that pass agrees on the grid
        // shape, which a viewport resize would otherwise change mid-loop.
        private int fixedAxisCount = 1;
        private Vector2 cellSize;

        private int lastFirst = -1;
        private int lastLast = -2;

        // Cell positions are expressed relative to the Content's own rect, so a Content
        // pivoted away from its leading corner shifts every cell when it resizes.
        private Vector2 lastContentSize;
        private bool layoutDirty;

        private bool snapArmed;
        private SnapTween snap;

        private readonly Dictionary<int, LoopGridViewItem> shown = new ();
        private readonly List<LoopGridViewItem> shownOrdered = new ();
        private readonly Dictionary<string, Stack<LoopGridViewItem>> pools = new ();

        // Indices the provider declined to build for the current window. Retrying
        // one every frame costs a delegate call and a failed lookup per frame.
        private readonly HashSet<int> knownNull = new ();

        private readonly List<int> recycleScratch = new ();
        private readonly List<int> orderScratch = new ();

        public IList<ItemPrefabConfData> ItemPrefabDataList => mItemPrefabDataList;

        public ListItemArrangeType ArrangeType => mArrangeType;

        public int ItemTotalCount => count;

        /// <summary>
        /// The currently materialized cells in ascending data-index order. With
        /// recycling this is only the visible window plus buffer, not every cell.
        /// </summary>
        public IList<LoopGridViewItem> ItemList => shownOrdered;

        public ScrollRect ScrollRect
        {
            get
            {
                EnsureScrollRect();
                return scrollRect;
            }
        }

        public RectTransform ViewPortTrans
        {
            get
            {
                EnsureScrollRect();
                return scrollRect != null ? scrollRect.viewport : transform as RectTransform;
            }
        }

        public float ViewPortHeight
        {
            get
            {
                RectTransform viewport = ViewPortTrans;
                return viewport != null ? viewport.rect.height : 0f;
            }
        }

        /// <summary>
        /// Binds the data source. <paramref name="provider"/> is invoked with
        /// this grid, a data index, and the resolved row and column whenever a
        /// cell needs to be materialized; it must return a cell created through
        /// <see cref="NewListViewItem"/>.
        /// </summary>
        public void InitGridView(int itemCount, Func<LoopGridView, int, int, int, LoopGridViewItem> provider)
        {
            provideItem = provider;
            EnsureScrollRect();

            scrollRect.onValueChanged.RemoveListener(OnScroll);
            scrollRect.onValueChanged.AddListener(OnScroll);

            SetListItemCount(itemCount, true);
        }

        public void SetListItemCount(int itemCount, bool resetPos = true)
        {
            if (itemCount < 0)
                itemCount = 0;

            EnsureScrollRect();

            // Data at an index that already existed may have changed, and several call
            // sites rely on this alone to redraw, so the window is rebound wholesale.
            // Recompute's own force pass does the recycling.
            count = itemCount;
            ResolveGridShape();
            UpdateContentSize();

            if (resetPos)
                SetScrollLeading(0f);

            CancelSnap();
            Recompute(force: true);
        }

        public void RefreshAllShownItem()
        {
            EnsureScrollRect();

            // Forcing a full pass drops every resident cell and rebinds the
            // visible window from the provider, picking up changed data.
            Recompute(force: true);
        }

        public void RefreshItemByItemIndex(int itemIndex)
        {
            if (itemIndex < 0 || itemIndex >= count)
                return;

            if (shown.TryGetValue(itemIndex, out LoopGridViewItem existing))
            {
                shown.Remove(itemIndex);
                ReturnToPool(existing);
            }

            knownNull.Remove(itemIndex);
            Recompute(force: false);
        }

        /// <summary>
        /// Returns a cell for <paramref name="itemPrefabName"/>, reusing a pooled
        /// instance when one is available and otherwise instantiating the
        /// registered prefab. Providers call this to obtain the cell they bind.
        /// </summary>
        public LoopGridViewItem NewListViewItem(string itemPrefabName)
        {
            EnsureScrollRect();

            LoopGridViewItem item = RentFromPool(itemPrefabName);

            if (item == null)
            {
                item = InstantiateCell(FindPrefabConf(itemPrefabName), itemPrefabName);

                if (item == null)
                    return null;
            }

            item.transform.SetParent(content, false);
            item.gameObject.SetActive(true);
            item.ItemPrefabName = itemPrefabName;
            return item;
        }

        public LoopGridViewItem GetShownItemByItemIndex(int index)
        {
            if (index < 0 || index >= count)
                return null;

            return shown.TryGetValue(index, out LoopGridViewItem item) ? item : null;
        }

        void IBeginDragHandler.OnBeginDrag(PointerEventData eventData) =>
            CancelSnap();

        void IEndDragHandler.OnEndDrag(PointerEventData eventData) =>
            snapArmed = mItemSnapEnable;

        private bool IsVertical => ScrollSpace.IsVertical(mArrangeType);

        private int MajorAxis => ScrollSpace.MajorAxis(mArrangeType);

        private int LineCount => count <= 0 ? 0 : (count + fixedAxisCount - 1) / fixedAxisCount;

        // How far past either viewport edge, along the scrolling axis, a line stays
        // resident before its cells are recycled.
        private float RecycleDistance => Mathf.Max(0f, IsVertical ? mItemRecycleDistance.y : mItemRecycleDistance.x);

        private float CellMajorSize => IsVertical ? cellSize.y : cellSize.x;

        // Cell stride along the major (scrolling) axis: cell size plus the
        // inter-cell spacing that follows it.
        private float CellMajorStride => CellMajorSize + MajorInterCellSpacing;

        // Cell stride along the minor (fixed) axis.
        private float CellMinorStride => (IsVertical ? cellSize.x : cellSize.y) + MinorInterCellSpacing;

        private float MajorPadLeading => mPadding == null ? 0f : IsVertical ? mPadding.top : mPadding.left;

        private float MajorPadTrailing => mPadding == null ? 0f : IsVertical ? mPadding.bottom : mPadding.right;

        private float MinorPadLeading => mPadding == null ? 0f : IsVertical ? mPadding.left : mPadding.top;

        private float MinorPadTrailing => mPadding == null ? 0f : IsVertical ? mPadding.right : mPadding.bottom;

        private float MajorInterCellSpacing => IsVertical ? mItemPadding.y : mItemPadding.x;

        private float MinorInterCellSpacing => IsVertical ? mItemPadding.x : mItemPadding.y;

        private float ContentExtent
        {
            get
            {
                int lines = LineCount;

                if (lines <= 0)
                    return 0f;

                // The last line carries no trailing inter-cell spacing.
                return MajorPadLeading + MajorPadTrailing + (lines * CellMajorStride) - MajorInterCellSpacing;
            }
        }

        private void Awake() =>
            EnsureScrollRect();

        // Editor-time only: a freshly added component gets its ScrollRect, Viewport
        // and Content. Initialization waits for the first runtime call, by which
        // time the serialized fields have been authored.
        private void Reset() =>
            EnsureStructure();

        private void OnEnable() =>
            Recompute(force: true);

        private void OnRectTransformDimensionsChange()
        {
            // Runs inside uGUI's layout pass; rebuilding the window from here would
            // reparent and resize rects mid-rebuild. Defer to the next frame instead.
            if (isActiveAndEnabled)
                layoutDirty = true;
        }

        private void OnScroll(Vector2 _) =>
            Recompute(force: false);

        private void LateUpdate()
        {
            Tick(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// One frame of deferred work: a layout change picked up during uGUI's
        /// rebuild, then the snap tween or the check that arms it.
        /// </summary>
        internal void Tick(float unscaledDeltaTime)
        {
            if (layoutDirty)
            {
                layoutDirty = false;
                int before = fixedAxisCount;
                ResolveGridShape();
                UpdateContentSize();

                // A viewport resize that changes how many cells fit across re-assigns
                // every index to a different row/column, so the bindings have to be remade.
                Recompute(force: fixedAxisCount != before);
            }

            if (snap.Active)
            {
                SetScrollLeading(snap.Step(unscaledDeltaTime));
                Recompute(force: false);
            }
            else if (snapArmed && scrollRect != null && scrollRect.velocity.sqrMagnitude <= SNAP_REST_SPEED_SQR)
            {
                snapArmed = false;
                BeginSnap();
            }
        }

        private void EnsureScrollRect()
        {
            EnsureStructure();
            EnsureInitialized();
        }

        // Gives the component its ScrollRect, Viewport and Content, inventing only
        // the parts the prefab does not already provide.
        private void EnsureStructure()
        {
            if (scrollRect != null && content != null)
                return;

            var createdScrollRect = false;

            if (scrollRect == null)
                scrollRect = GetComponent<ScrollRect>();

            if (scrollRect == null)
            {
                scrollRect = gameObject.AddComponent<ScrollRect>();
                createdScrollRect = true;
            }

            // A prefab-authored ScrollRect already states which axes may move — one of
            // the shipped views deliberately locks both — so only a ScrollRect this
            // component invented gets its axes derived from the arrange type.
            if (createdScrollRect)
            {
                scrollRect.horizontal = !IsVertical;
                scrollRect.vertical = IsVertical;
            }

            if (scrollRect.viewport == null)
            {
                var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
                var viewportRt = viewportGo.GetComponent<RectTransform>();
                viewportRt.SetParent(transform, false);
                viewportRt.anchorMin = Vector2.zero;
                viewportRt.anchorMax = Vector2.one;
                viewportRt.offsetMin = Vector2.zero;
                viewportRt.offsetMax = Vector2.zero;
                scrollRect.viewport = viewportRt;
            }

            bool createdContent = scrollRect.content == null;

            if (createdContent)
            {
                var contentGo = new GameObject("Content", typeof(RectTransform));
                var contentRt = contentGo.GetComponent<RectTransform>();
                contentRt.SetParent(scrollRect.viewport, false);
                scrollRect.content = contentRt;
            }

            content = scrollRect.content;

            // An authored Content carries anchors, a pivot and a cross-axis size the
            // designer chose; only one this component invented has none to preserve.
            if (createdContent)
                ConfigureContentAnchors();

            StripAutoLayout();
        }

        private void EnsureInitialized()
        {
            if (initialized || content == null)
                return;

            initialized = true;
            ResolveGridShape();
            DeactivateInHierarchyTemplates();
            PrewarmPools();
        }

        // An item template that lives inside this view's own hierarchy (rather than
        // being an asset reference) is a real, active GameObject: left alone it draws
        // as a stray cell on top of the grid.
        private void DeactivateInHierarchyTemplates()
        {
            for (int i = 0; i < mItemPrefabDataList.Count; i++)
            {
                ItemPrefabConfData conf = mItemPrefabDataList[i];

                if (conf?.mItemPrefab == null)
                    continue;

                Transform template = conf.mItemPrefab.transform;

                if (template != transform && template.IsChildOf(transform))
                    conf.mItemPrefab.SetActive(false);
            }
        }

        private void PrewarmPools()
        {
            for (int i = 0; i < mItemPrefabDataList.Count; i++)
            {
                ItemPrefabConfData conf = mItemPrefabDataList[i];

                if (conf?.mItemPrefab == null || conf.mInitCreateCount <= 0)
                    continue;

                string prefabName = conf.mItemPrefab.name;

                for (int n = 0; n < conf.mInitCreateCount; n++)
                {
                    LoopGridViewItem cell = InstantiateCell(conf, prefabName);

                    if (cell == null)
                        break;

                    cell.ItemPrefabName = prefabName;
                    ReturnToPool(cell);
                }
            }
        }

        private LoopGridViewItem InstantiateCell(ItemPrefabConfData conf, string prefabName)
        {
            if (conf == null || conf.mItemPrefab == null)
            {
                Debug.LogWarning($"[SuperScrollView] No grid prefab named '{prefabName}' is registered on {name}.");
                return null;
            }

            GameObject go = Instantiate(conf.mItemPrefab, content, false);
            var item = go.GetComponent<LoopGridViewItem>();

            return item != null ? item : go.AddComponent<LoopGridViewItem>();
        }

        /// <summary>
        /// Resolves the two things every index in a pass has to agree on: the cell
        /// size, and how many cells sit across the minor axis.
        ///
        /// <c>mItemSize</c> is authored as (0,0) on at least one shipped grid, which
        /// collapses every cell and leaves the grid blank, so each component falls
        /// back independently to the item prefab's own rect. With
        /// <c>mGridFixedType</c> 1 the cells-across count comes from the space the
        /// viewport actually offers: n cells and n-1 gaps fit when
        /// <c>n * cell + (n-1) * gap &lt;= available</c>, i.e.
        /// <c>n = floor((available + gap) / (cell + gap))</c>.
        /// </summary>
        private void ResolveGridShape()
        {
            cellSize = ResolveCellSize();
            fixedAxisCount = Mathf.Max(1, mFixedRowOrColumnCount);

            if (mGridFixedType != 1 || scrollRect == null || scrollRect.viewport == null)
                return;

            float stride = CellMinorStride;

            if (stride <= 0f)
                return;

            Rect viewport = scrollRect.viewport.rect;
            float available = (IsVertical ? viewport.width : viewport.height) - MinorPadLeading - MinorPadTrailing;

            if (available <= 0f)
                return;

            fixedAxisCount = Mathf.Max(1, Mathf.FloorToInt((available + MinorInterCellSpacing) / stride));
        }

        private Vector2 ResolveCellSize()
        {
            Vector2 resolved = mItemSize;

            if (resolved.x > 0f && resolved.y > 0f)
                return resolved;

            for (int i = 0; i < mItemPrefabDataList.Count; i++)
            {
                ItemPrefabConfData conf = mItemPrefabDataList[i];

                if (conf?.mItemPrefab == null || conf.mItemPrefab.transform is not RectTransform prefabRt)
                    continue;

                Rect r = prefabRt.rect;

                if (resolved.x <= 0f && r.width > 0f)
                    resolved.x = r.width;

                if (resolved.y <= 0f && r.height > 0f)
                    resolved.y = r.height;

                if (resolved.x > 0f && resolved.y > 0f)
                    break;
            }

            return resolved;
        }

        // Anchor and pin a Content this component created to the arrange axis's
        // leading side with a stretch on the cross axis.
        private void ConfigureContentAnchors()
        {
            if (content == null)
                return;

            switch (mArrangeType)
            {
                case ListItemArrangeType.TopToBottom:
                    content.anchorMin = new Vector2(0f, 1f);
                    content.anchorMax = new Vector2(1f, 1f);
                    content.pivot = new Vector2(0.5f, 1f);
                    break;
                case ListItemArrangeType.BottomToTop:
                    content.anchorMin = new Vector2(0f, 0f);
                    content.anchorMax = new Vector2(1f, 0f);
                    content.pivot = new Vector2(0.5f, 0f);
                    break;
                case ListItemArrangeType.LeftToRight:
                    content.anchorMin = new Vector2(0f, 0f);
                    content.anchorMax = new Vector2(0f, 1f);
                    content.pivot = new Vector2(0f, 0.5f);
                    break;
                case ListItemArrangeType.RightToLeft:
                    content.anchorMin = new Vector2(1f, 0f);
                    content.anchorMax = new Vector2(1f, 1f);
                    content.pivot = new Vector2(1f, 0.5f);
                    break;
            }

            content.anchoredPosition = Vector2.zero;
        }

        private void StripAutoLayout()
        {
            if (content == null)
                return;

            DestroyIfPresent(content.GetComponent<GridLayoutGroup>());
            DestroyIfPresent(content.GetComponent<VerticalLayoutGroup>());
            DestroyIfPresent(content.GetComponent<HorizontalLayoutGroup>());
            DestroyIfPresent(content.GetComponent<ContentSizeFitter>());
        }

        private static void DestroyIfPresent(Component component)
        {
            if (component != null)
                Destroy(component);
        }

        private ItemPrefabConfData FindPrefabConf(string prefabName)
        {
            for (int i = 0; i < mItemPrefabDataList.Count; i++)
            {
                ItemPrefabConfData conf = mItemPrefabDataList[i];

                if (conf?.mItemPrefab != null && conf.mItemPrefab.name == prefabName)
                    return conf;
            }

            return null;
        }

        private void UpdateContentSize()
        {
            if (content == null || scrollRect == null || scrollRect.viewport == null)
                return;

            int axis = MajorAxis;
            Vector2 size = content.sizeDelta;
            float target = ScrollSpace.SizeDeltaForExtent(content, scrollRect.viewport.rect, axis, ContentExtent);

            if (axis == 0)
                size.x = target;
            else
                size.y = target;

            if (size != content.sizeDelta)
                content.sizeDelta = size;
        }

        private float ViewportMajorSize()
        {
            Rect r = scrollRect.viewport.rect;
            return IsVertical ? r.height : r.width;
        }

        private float ScrollLeading() =>
            content == null || scrollRect == null || scrollRect.viewport == null
                ? 0f
                : ScrollSpace.LeadingOfContent(mArrangeType, content, scrollRect.viewport.rect);

        private void SetScrollLeading(float leading)
        {
            if (content == null || scrollRect == null || scrollRect.viewport == null)
                return;

            ScrollSpace.SetLeadingOfContent(mArrangeType, content, scrollRect.viewport.rect, leading);
        }

        private void Recompute(bool force)
        {
            EnsureScrollRect();

            if (content == null || count <= 0)
            {
                if (force)
                    RecycleAllShown();

                return;
            }

            float viewport = ViewportMajorSize();
            float stride = CellMajorStride;
            int lines = LineCount;

            if (viewport <= 0f || stride <= 0f || lines <= 0)
            {
                if (force)
                    RecycleAllShown();

                return;
            }

            int fixedCount = fixedAxisCount;
            float keep = RecycleDistance;
            float leading = ScrollLeading() - MajorPadLeading - keep;
            float trailing = leading + viewport + (2f * keep);

            int firstLine = Mathf.Max(0, Mathf.FloorToInt(leading / stride));
            int lastLine = Mathf.Min(lines - 1, Mathf.FloorToInt(trailing / stride));

            int first = firstLine * fixedCount;
            int last = Mathf.Min(count - 1, ((lastLine + 1) * fixedCount) - 1);

            if (force)
                RecycleAllShown();
            else
                RecycleOutside(first, last);

            var created = false;

            for (int i = first; i <= last; i++)
            {
                if (shown.ContainsKey(i) || knownNull.Contains(i) || provideItem == null)
                    continue;

                ResolveRowColumn(i, fixedCount, out int row, out int column);

                LoopGridViewItem cell = provideItem(this, i, row, column);

                if (cell == null)
                {
                    knownNull.Add(i);
                    continue;
                }

                cell.ItemIndex = i;
                cell.RowIndex = row;
                cell.ColumnIndex = column;
                cell.transform.SetParent(content, false);
                cell.gameObject.SetActive(true);
                shown[i] = cell;
                created = true;
            }

            Rect contentRect = content.rect;
            var contentSize = new Vector2(contentRect.width, contentRect.height);

            if (!created && first == lastFirst && last == lastLast && contentSize == lastContentSize)
                return;

            RebuildOrdered();
            PositionShown();
            lastFirst = first;
            lastLast = last;
            lastContentSize = contentSize;
        }

        private void ResolveRowColumn(int dataIndex, int fixedCount, out int row, out int column)
        {
            int line = dataIndex / fixedCount;
            int slot = dataIndex % fixedCount;

            if (IsVertical)
            {
                row = line;
                column = slot;
            }
            else
            {
                column = line;
                row = slot;
            }
        }

        private void PositionShown()
        {
            int fixedCount = fixedAxisCount;
            float majorStride = CellMajorStride;
            float minorStride = CellMinorStride;
            float majorPad = MajorPadLeading;
            float minorPad = MinorPadLeading;
            Rect contentRect = content.rect;

            for (int i = 0; i < shownOrdered.Count; i++)
            {
                LoopGridViewItem cell = shownOrdered[i];

                if (cell == null)
                    continue;

                var rt = cell.transform as RectTransform;

                if (rt == null)
                    continue;

                int line = cell.ItemIndex / fixedCount;
                int slot = cell.ItemIndex % fixedCount;

                Place(rt, contentRect, majorPad + (line * majorStride), minorPad + (slot * minorStride));

                // Pooled cells come back in arbitrary sibling order; ascending order is
                // what makes overlapping cells stack the way the data reads.
                if (rt.GetSiblingIndex() != i)
                    rt.SetSiblingIndex(i);
            }
        }

        // Offsets the cell so its leading corner sits `major` px along the scrolling
        // axis and `minor` px across, deriving the anchored position from the cell's
        // own anchors and pivot rather than replacing them.
        private void Place(RectTransform rt, Rect contentRect, float major, float minor)
        {
            int majorAxis = MajorAxis;
            int minorAxis = 1 - majorAxis;

            // Down and leftwards are measured from the content's max edge; up and
            // rightwards from its min edge.
            bool majorFromMax = mArrangeType == ListItemArrangeType.TopToBottom
                                || mArrangeType == ListItemArrangeType.RightToLeft;
            bool minorFromMax = majorAxis == 0;

            // Sized first: the anchored position is derived from the cell's sizeDelta,
            // so resizing after placing would leave it off by the size change.
            ApplyCellSize(rt);

            ScrollSpace.SetAnchoredComponent(rt, majorAxis,
                ScrollSpace.AnchoredForEdge(rt, contentRect, majorAxis, major, majorFromMax));

            ScrollSpace.SetAnchoredComponent(rt, minorAxis,
                ScrollSpace.AnchoredForEdge(rt, contentRect, minorAxis, minor, minorFromMax));
        }

        // Only the components mItemSize actually specifies are written; where it is
        // unset the prefab's authored size stands, which is also what the stride was
        // resolved from.
        private void ApplyCellSize(RectTransform rt)
        {
            Vector2 size = rt.sizeDelta;

            if (mItemSize.x > 0f && !ScrollSpace.StretchesOn(rt, 0))
                size.x = mItemSize.x;

            if (mItemSize.y > 0f && !ScrollSpace.StretchesOn(rt, 1))
                size.y = mItemSize.y;

            if (size != rt.sizeDelta)
                rt.sizeDelta = size;
        }

        private void RebuildOrdered()
        {
            shownOrdered.Clear();
            orderScratch.Clear();

            foreach (int index in shown.Keys)
                orderScratch.Add(index);

            orderScratch.Sort();

            for (int i = 0; i < orderScratch.Count; i++)
            {
                if (shown.TryGetValue(orderScratch[i], out LoopGridViewItem cell) && cell != null)
                    shownOrdered.Add(cell);
            }
        }

        private void RecycleOutside(int first, int last)
        {
            recycleScratch.Clear();

            foreach (KeyValuePair<int, LoopGridViewItem> entry in shown)
            {
                if (entry.Key < first || entry.Key > last)
                    recycleScratch.Add(entry.Key);
            }

            for (int i = 0; i < recycleScratch.Count; i++)
            {
                int index = recycleScratch[i];

                if (shown.TryGetValue(index, out LoopGridViewItem cell))
                {
                    shown.Remove(index);
                    ReturnToPool(cell);
                }
            }

            recycleScratch.Clear();

            foreach (int index in knownNull)
            {
                if (index < first || index > last)
                    recycleScratch.Add(index);
            }

            for (int i = 0; i < recycleScratch.Count; i++)
                knownNull.Remove(recycleScratch[i]);
        }

        private void RecycleAllShown()
        {
            foreach (KeyValuePair<int, LoopGridViewItem> entry in shown)
                ReturnToPool(entry.Value);

            shown.Clear();
            shownOrdered.Clear();
            knownNull.Clear();
            lastFirst = -1;
            lastLast = -2;
        }

        private LoopGridViewItem RentFromPool(string prefabName)
        {
            if (!pools.TryGetValue(prefabName, out Stack<LoopGridViewItem> stack))
                return null;

            while (stack.Count > 0)
            {
                LoopGridViewItem cell = stack.Pop();

                if (cell != null)
                    return cell;
            }

            return null;
        }

        private void ReturnToPool(LoopGridViewItem cell)
        {
            if (cell == null)
                return;

            string key = cell.ItemPrefabName ?? string.Empty;

            if (!pools.TryGetValue(key, out Stack<LoopGridViewItem> stack))
            {
                stack = new Stack<LoopGridViewItem>();
                pools[key] = stack;
            }

            cell.gameObject.SetActive(false);
            stack.Push(cell);
        }

        // --- snapping ------------------------------------------------------

        private void CancelSnap()
        {
            snapArmed = false;
            snap.Cancel();
        }

        /// <summary>
        /// Aligns the nearest line's snap pivot with the viewport's snap pivot, both
        /// resolved to a fraction of the respective extent from the leading side by
        /// <see cref="ScrollSpace.SnapFactor"/>. Lines are uniform, so the candidate
        /// is found by division rather than a search.
        /// </summary>
        private void BeginSnap()
        {
            if (count <= 0 || content == null || scrollRect == null)
                return;

            float viewport = ViewportMajorSize();
            float stride = CellMajorStride;
            int lines = LineCount;

            if (viewport <= 0f || stride <= 0f || lines <= 0)
                return;

            float viewportFactor = ScrollSpace.SnapFactor(mArrangeType, mViewPortSnapPivot);
            float itemFactor = ScrollSpace.SnapFactor(mArrangeType, mItemSnapPivot);
            float itemPivot = itemFactor * CellMajorSize;
            float leading = ScrollLeading();
            float reference = leading + (viewportFactor * viewport);

            int candidate = Mathf.Clamp(Mathf.FloorToInt((reference - MajorPadLeading) / stride), 0, lines - 1);
            float best = float.MaxValue;
            var bestLine = -1;

            for (int line = Mathf.Max(0, candidate - 1); line <= Mathf.Min(lines - 1, candidate + 1); line++)
            {
                float distance = Mathf.Abs(LineLeading(line) + itemPivot - reference);

                if (distance >= best)
                    continue;

                best = distance;
                bestLine = line;
            }

            if (bestLine < 0)
                return;

            float maxScroll = Mathf.Max(0f, ContentExtent - viewport);
            float target = Mathf.Clamp(LineLeading(bestLine) + itemPivot - (viewportFactor * viewport), 0f, maxScroll);

            if (Mathf.Abs(target - leading) < 1f)
                return;

            snap.Begin(leading, target);
            scrollRect.velocity = Vector2.zero;
        }

        private float LineLeading(int line) =>
            MajorPadLeading + (line * CellMajorStride);
    }
}
