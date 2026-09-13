using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SuperScrollView
{
    /// <summary>
    /// A recycling single-axis list built on a uGUI <see cref="ScrollRect"/>.
    /// Rather than instantiating one GameObject per data row, it keeps only the
    /// rows whose extent overlaps the viewport — plus a small leading/trailing
    /// buffer — materialized at once, returning off-screen rows to per-prefab
    /// pools and rebinding them as new indices scroll into view.
    ///
    /// Row extents may vary: each index starts from an estimate and is replaced
    /// by the row's real laid-out size the first time it materializes, so the
    /// content size and scrollbar stay accurate. The Content transform is driven
    /// directly (anchored position only) with no LayoutGroup or
    /// ContentSizeFitter in the loop.
    ///
    /// Layout writes anchored positions and the Content's major-axis size, and
    /// nothing else: anchors, pivots and authored sizes on both the rows and the
    /// Content are left exactly as the prefab set them. See
    /// <see cref="ScrollSpace"/> for the coordinate model and why that matters.
    ///
    /// The serialized field names match the layout the prefabs were authored
    /// against so existing prefab YAML binds unchanged.
    /// </summary>
    [DisallowMultipleComponent]
    public class LoopListView2 : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        [SerializeField] private List<ItemPrefabConfData> mItemPrefabDataList = new ();
        [SerializeField] private ListItemArrangeType mArrangeType = ListItemArrangeType.TopToBottom;
        [SerializeField] private bool mSupportScrollBar = true;
        [SerializeField] private bool mItemSnapEnable;
        [SerializeField] private Vector2 mViewPortSnapPivot;
        [SerializeField] private Vector2 mItemSnapPivot;

        // Rows kept resident on each side of the visible window so a fast fling
        // finds them already bound instead of popping in mid-frame.
        private const int BUFFER_ITEMS = 2;

        // Seed extent used before any prefab hint or real measurement is known.
        private const float DEFAULT_ITEM_SIZE = 100f;

        // Re-seeding unmeasured rows shifts every offset, so it is worth doing only
        // when the running average has drifted out of this band around the seed.
        private const float RESEED_BAND = 0.1f;

        // A re-seed can leave the window under-filled; each extra pass strictly
        // improves the estimate, so a small bound is enough to settle in one frame.
        private const int MAX_SETTLE_PASSES = 4;

        // A programmatic move re-aims until the target row sits within this many px
        // of where it was asked to be.
        private const float SETTLE_EPSILON = 0.5f;

        // Squared px/s below which the ScrollRect's inertia counts as spent. Snapping
        // waits for that instead of firing on end-of-drag, so a flick still flings.
        private const float SNAP_REST_SPEED_SQR = 400f;

        private ScrollRect scrollRect;
        private RectTransform content;
        private int count;
        private Func<LoopListView2, int, LoopListViewItem2> provideItem;
        private bool initialized;

        // Major-axis extent of each index, including its row Padding. Seeded from
        // an estimate and refined to the measured value once a row is laid out.
        // Length is a capacity, not the count: entries past `count` are stale.
        private float[] itemSize = Array.Empty<float>();

        // Parallel to itemSize: true once the row's real laid-out size replaced the
        // estimate, which is what protects it from being re-seeded.
        private bool[] measured = Array.Empty<bool>();

        // Prefix sums of itemSize: offset[i] is the leading edge of index i in
        // content space, so offset[count] is the total content extent. offset[0] is
        // the list's leading inset rather than 0.
        private float[] offset = { 0f };

        // First index whose prefix sum is stale. Measuring a row only invalidates the
        // suffix after it, so the rebuild is deferred and done once per pass instead
        // of once per measured row.
        private int offsetsDirtyFrom = int.MaxValue;

        private float seedEstimate;
        private float measuredTotal;
        private int measuredCount;
        private float startPad;

        private int lastFirst = -1;
        private int lastLast = -2;

        // Item positions are expressed relative to the Content's own rect, so a Content
        // pivoted away from its leading edge shifts every row when it resizes.
        private Vector2 lastContentSize;
        private bool layoutDirty;

        private bool snapArmed;
        private SnapTween snap;

        private readonly Dictionary<int, LoopListViewItem2> shown = new ();
        private readonly List<LoopListViewItem2> shownOrdered = new ();
        private readonly Dictionary<string, Stack<LoopListViewItem2>> pools = new ();

        // Indices the provider declined to build for the current window. Retrying
        // one every frame costs a delegate call and a failed lookup per frame.
        private readonly HashSet<int> knownNull = new ();

        private readonly List<int> recycleScratch = new ();
        private readonly List<int> orderScratch = new ();

        public IList<ItemPrefabConfData> ItemPrefabDataList => mItemPrefabDataList;

        public ListItemArrangeType ArrangeType => mArrangeType;

        public int ItemTotalCount => count;

        /// <summary>
        /// The currently materialized rows in ascending data-index order. With
        /// recycling this is only the visible window plus buffer, not every row.
        /// </summary>
        public IList<LoopListViewItem2> ItemList => shownOrdered;

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
        /// this view and a data index whenever a row needs to be materialized;
        /// it must return a row created through <see cref="NewListViewItem"/>.
        /// </summary>
        public void InitListView(int initialItemCount, Func<LoopListView2, int, LoopListViewItem2> provider)
        {
            provideItem = provider;
            EnsureScrollRect();

            scrollRect.onValueChanged.RemoveListener(OnScroll);
            scrollRect.onValueChanged.AddListener(OnScroll);

            SetListItemCount(initialItemCount, true);
        }

        public void SetListItemCount(int itemCount, bool resetPos = true)
        {
            if (itemCount < 0)
                itemCount = 0;

            EnsureScrollRect();

            // Data at an index that already existed may have changed, and several call
            // sites rely on this alone to redraw, so the window is rebound wholesale.
            // Recompute's own force pass does the recycling; doing it here as well
            // would return every row to the pool twice.
            ResizeMetrics(itemCount);
            FlushOffsets();
            UpdateContentSize();

            if (resetPos)
                SetScrollLeading(0f);

            CancelSnap();
            Recompute(force: true);
        }

        public void RefreshAllShownItem()
        {
            EnsureScrollRect();

            // Forcing a full pass drops every resident row and rebinds the
            // visible window from the provider, picking up changed data.
            Recompute(force: true);
        }

        public void RefreshItemByItemIndex(int itemIndex)
        {
            if (itemIndex < 0 || itemIndex >= count)
                return;

            if (shown.TryGetValue(itemIndex, out LoopListViewItem2 existing))
            {
                shown.Remove(itemIndex);
                ReturnToPool(existing);
            }

            knownNull.Remove(itemIndex);
            Recompute(force: false);
        }

        /// <summary>
        ///     Rewinds to the leading edge and rebinds the visible window. The item
        ///     count is left untouched, so a view that is reset keeps showing its rows.
        /// </summary>
        public void ResetListView()
        {
            EnsureScrollRect();
            SetScrollLeading(0f);
            CancelSnap();
            Recompute(force: true);
        }

        public void DoActionForEachShownItem(Action<LoopListViewItem2, object> action, object param = null)
        {
            if (action == null)
                return;

            EnsureScrollRect();

            for (int i = 0; i < shownOrdered.Count; i++)
                action(shownOrdered[i], param);
        }

        /// <summary>
        /// Returns a row for <paramref name="itemPrefabName"/>, reusing a pooled
        /// instance when one is available and otherwise instantiating the
        /// registered prefab. Providers call this to obtain the row they bind.
        /// </summary>
        public LoopListViewItem2 NewListViewItem(string itemPrefabName)
        {
            EnsureScrollRect();

            ItemPrefabConfData conf = FindPrefabConf(itemPrefabName);
            LoopListViewItem2 item = RentFromPool(itemPrefabName);

            if (item == null)
            {
                item = InstantiateRow(conf, itemPrefabName);

                if (item == null)
                    return null;
            }

            item.transform.SetParent(content, false);
            item.gameObject.SetActive(true);
            item.ItemPrefabName = itemPrefabName;
            item.Padding = conf != null ? conf.mPadding : 0f;
            item.ArrangeVertical = ScrollSpace.IsVertical(mArrangeType);
            item.MeasuredSize = 0f;
            return item;
        }

        public LoopListViewItem2 GetShownItemByItemIndex(int index)
        {
            if (index < 0 || index >= count)
                return null;

            return shown.TryGetValue(index, out LoopListViewItem2 item) ? item : null;
        }

        /// <summary>
        /// Scrolls so the leading edge of <paramref name="index"/> sits
        /// <paramref name="offsetToViewport"/> pixels past the viewport's
        /// leading edge, clamped to the scrollable range.
        /// </summary>
        public void MovePanelToItemIndex(int index, float offsetToViewport)
        {
            EnsureScrollRect();

            if (count <= 0 || content == null || scrollRect == null)
                return;

            int clamped = Mathf.Clamp(index, 0, count - 1);
            CancelSnap();

            // Rows between the resident window and the target are on estimates until
            // they materialize, so the first move can land off by their measurement
            // error; each further pass re-aims from the offsets measured by the last.
            for (var pass = 0; pass < MAX_SETTLE_PASSES; pass++)
            {
                float viewport = ViewportMajorSize();
                float maxScroll = Mathf.Max(0f, ContentExtent - viewport);
                float target = Mathf.Clamp(LeadingOf(clamped) - offsetToViewport, 0f, maxScroll);

                if (pass > 0 && Mathf.Abs(target - ScrollLeading()) <= SETTLE_EPSILON)
                    return;

                SetScrollLeading(target);

                // Only the window moved; the bindings are still valid, so rebinding
                // every resident row here would throw away the pool for nothing.
                Recompute(force: false);
            }
        }

        /// <summary>
        /// Moves the Content by <paramref name="delta"/> along the arrange axis,
        /// measured in the Content's own anchored-position space: positive is
        /// towards +Y for a vertical list and +X for a horizontal one, whichever
        /// way the list runs. Negating the extent of rows inserted at the leading
        /// end therefore keeps the rows already on screen stationary.
        /// </summary>
        public void MovePanelByOffset(float delta)
        {
            EnsureScrollRect();

            if (scrollRect == null || content == null)
                return;

            CancelSnap();
            SetScrollLeading(ScrollLeading() + (delta * ScrollSpace.LeadingSign(mArrangeType)));
            Recompute(force: false);
        }

        void IBeginDragHandler.OnBeginDrag(PointerEventData eventData) =>
            CancelSnap();

        void IEndDragHandler.OnEndDrag(PointerEventData eventData) =>
            snapArmed = mItemSnapEnable;

        private bool IsVertical => ScrollSpace.IsVertical(mArrangeType);

        private int MajorAxis => ScrollSpace.MajorAxis(mArrangeType);

        private float ContentExtent => count > 0 ? offset[count] : 0f;

        private float LeadingOf(int index) =>
            index <= 0 ? offset[0] : (index <= count ? offset[index] : ContentExtent);

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
                UpdateContentSize();
                Recompute(force: false);
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

            bool createdScrollRect = false;

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
            startPad = mItemPrefabDataList.Count > 0 && mItemPrefabDataList[0] != null
                ? mItemPrefabDataList[0].mStartPosOffset
                : 0f;
            seedEstimate = PrefabSeedEstimate();
            offset[0] = startPad;

            ApplyScrollBarSupport();
            DeactivateInHierarchyTemplates();
            PrewarmPools();
        }

        // An item template that lives inside this view's own hierarchy (rather than
        // being an asset reference) is a real, active GameObject: left alone it draws
        // as a stray row on top of the list.
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
                    LoopListViewItem2 row = InstantiateRow(conf, prefabName);

                    if (row == null)
                        break;

                    row.ItemPrefabName = prefabName;
                    ReturnToPool(row);
                }
            }
        }

        private LoopListViewItem2 InstantiateRow(ItemPrefabConfData conf, string prefabName)
        {
            if (conf == null || conf.mItemPrefab == null)
            {
                Debug.LogWarning($"[SuperScrollView] No list prefab named '{prefabName}' is registered on {name}.");
                return null;
            }

            GameObject go = Instantiate(conf.mItemPrefab, content, false);
            var item = go.GetComponent<LoopListViewItem2>();

            return item != null ? item : go.AddComponent<LoopListViewItem2>();
        }

        private void ApplyScrollBarSupport()
        {
            if (mSupportScrollBar || scrollRect == null)
                return;

            if (scrollRect.verticalScrollbar != null)
                scrollRect.verticalScrollbar.gameObject.SetActive(false);

            if (scrollRect.horizontalScrollbar != null)
                scrollRect.horizontalScrollbar.gameObject.SetActive(false);
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

        // Manual positioning is incompatible with auto-layout drivers on the
        // Content node; remove any that slipped in.
        private void StripAutoLayout()
        {
            if (content == null)
                return;

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

        // The smallest registered row, not the largest: an estimate below the truth
        // makes the window cover more indices than needed, and each of those is then
        // measured and corrected. An estimate above the truth leaves the tail of the
        // viewport empty with nothing scheduled to fill it.
        private float PrefabSeedEstimate()
        {
            float estimate = 0f;
            var found = false;

            for (int i = 0; i < mItemPrefabDataList.Count; i++)
            {
                ItemPrefabConfData conf = mItemPrefabDataList[i];

                if (conf == null)
                    continue;

                float hint = conf.mItemSize;

                if (hint <= 0f && conf.mItemPrefab != null && conf.mItemPrefab.transform is RectTransform prefabRt)
                {
                    Rect r = prefabRt.rect;
                    hint = IsVertical ? r.height : r.width;
                }

                if (hint <= 0f)
                    continue;

                hint += conf.mPadding;

                if (!found || hint < estimate)
                {
                    estimate = hint;
                    found = true;
                }
            }

            return found ? estimate : DEFAULT_ITEM_SIZE;
        }

        private void ResizeMetrics(int newCount)
        {
            int old = count;
            count = newCount;
            EnsureMetricsCapacity(newCount);

            if (newCount > old)
            {
                for (int i = old; i < newCount; i++)
                {
                    itemSize[i] = seedEstimate;
                    measured[i] = false;
                }

                MarkOffsetsDirtyFrom(old);
            }
            else if (newCount < old)
            {
                for (int i = newCount; i < old; i++)
                {
                    if (!measured[i])
                        continue;

                    measuredTotal -= itemSize[i];
                    measuredCount--;
                    measured[i] = false;
                }

                MarkOffsetsDirtyFrom(newCount);
            }
        }

        private void EnsureMetricsCapacity(int needed)
        {
            if (itemSize.Length >= needed && offset.Length >= needed + 1)
                return;

            int capacity = Mathf.Max(needed, Mathf.Max(itemSize.Length * 2, 8));
            Array.Resize(ref itemSize, capacity);
            Array.Resize(ref measured, capacity);
            Array.Resize(ref offset, capacity + 1);
        }

        private void MarkOffsetsDirtyFrom(int index)
        {
            if (index < offsetsDirtyFrom)
                offsetsDirtyFrom = index;
        }

        // Rebuilds only the stale suffix: offset[dirtyFrom] is the prefix through the
        // indices before it, which by construction did not change.
        private void FlushOffsets()
        {
            if (offsetsDirtyFrom > count)
                return;

            EnsureMetricsCapacity(count);
            int from = Mathf.Max(0, offsetsDirtyFrom);

            if (from == 0)
                offset[0] = startPad;

            float acc = offset[from];

            for (int i = from; i < count; i++)
            {
                acc += itemSize[i];
                offset[i + 1] = acc;
            }

            offsetsDirtyFrom = int.MaxValue;
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

        /// <summary>
        /// Largest index whose leading edge is at or before <paramref name="at"/>.
        /// With <paramref name="firstOfRun"/> the search walks back to the first index
        /// of a run of equal offsets, so a stretch of zero-extent rows enters the
        /// window as a whole rather than only its last member.
        /// </summary>
        private int IndexAtOffset(float at, bool firstOfRun)
        {
            int lo = 0;
            int hi = count;

            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;

                if (offset[mid] <= at)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            int index = Mathf.Clamp(lo - 1, 0, count - 1);

            if (firstOfRun)
            {
                while (index > 0 && offset[index] == offset[index - 1])
                    index--;
            }

            return index;
        }

        private void Recompute(bool force)
        {
            for (var pass = 0; pass < MAX_SETTLE_PASSES; pass++)
            {
                if (!RecomputePass(force && pass == 0))
                    return;
            }
        }

        // Returns true when the settled metrics left the window mis-sized and another
        // pass is warranted.
        private bool RecomputePass(bool force)
        {
            EnsureScrollRect();

            if (content == null || count <= 0)
            {
                if (force)
                    RecycleAllShown();

                return false;
            }

            float viewport = ViewportMajorSize();

            if (viewport <= 0f)
            {
                if (force)
                    RecycleAllShown();

                return false;
            }

            float leading = ScrollLeading();
            float trailing = leading + viewport;

            int first = Mathf.Max(0, IndexAtOffset(leading, true) - BUFFER_ITEMS);
            int last = Mathf.Min(count - 1, IndexAtOffset(trailing, false) + BUFFER_ITEMS);

            // Whatever this pass does to the metrics, the row at `first` has to stay
            // where it is on screen; re-seeding shifts the offsets underneath it.
            float anchorBefore = LeadingOf(first);

            if (force)
                RecycleAllShown();
            else
                RecycleOutside(first, last);

            var created = false;
            var reseeded = false;
            float scanEnd = anchorBefore;
            var pastViewport = 0;

            for (int i = first; i <= last; i++)
            {
                if (!shown.ContainsKey(i) && !knownNull.Contains(i) && provideItem != null)
                {
                    LoopListViewItem2 row = provideItem(this, i);

                    if (row == null)
                        knownNull.Add(i);
                    else
                    {
                        row.ItemIndex = i;
                        row.transform.SetParent(content, false);
                        row.gameObject.SetActive(true);
                        shown[i] = row;
                        created = true;

                        // Position before measuring: a row laid out at its final place in
                        // the list is the one whose wrapped text height is meaningful.
                        Place(row.transform as RectTransform, LeadingOf(i));

                        if (Measure(i, row, !reseeded))
                            reseeded = true;
                    }
                }

                // Walk the real extents alongside the loop: with a deliberately low seed
                // the binary search can name far more indices than actually fit, and
                // this stops materializing once the viewport plus its buffer is covered.
                scanEnd += itemSize[i];

                if (scanEnd <= trailing)
                    continue;

                if (++pastViewport > BUFFER_ITEMS)
                {
                    last = i;
                    break;
                }
            }

            bool offsetsChanged = offsetsDirtyFrom <= count;
            FlushOffsets();

            if (offsetsChanged)
            {
                UpdateContentSize();
                float shift = LeadingOf(first) - anchorBefore;

                if (shift != 0f)
                {
                    leading += shift;
                    SetScrollLeading(leading);
                }
            }

            Rect contentRect = content.rect;
            var contentSize = new Vector2(contentRect.width, contentRect.height);

            if (created || offsetsChanged || first != lastFirst || last != lastLast || contentSize != lastContentSize)
            {
                RebuildOrdered();
                PositionShown();
            }

            lastFirst = first;
            lastLast = last;
            lastContentSize = contentSize;

            if (!offsetsChanged)
                return false;

            trailing = leading + viewport;

            return Mathf.Max(0, IndexAtOffset(leading, true) - BUFFER_ITEMS) != first
                   || Mathf.Min(count - 1, IndexAtOffset(trailing, false) + BUFFER_ITEMS) > last;
        }

        /// <summary>
        /// Refines index i's cached extent from the row's real laid-out size. Returns
        /// true when the running average was applied to the still-unmeasured entries.
        /// </summary>
        private bool Measure(int index, LoopListViewItem2 row, bool reseedAllowed)
        {
            var rt = row.transform as RectTransform;

            if (rt == null)
                return false;

            int axis = MajorAxis;

            // A row stretched along the arrange axis takes its extent from the Content
            // this measurement is used to size — reading it back would feed the content
            // extent into itself.
            if (ScrollSpace.StretchesOn(rt, axis))
                return false;

            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            Rect r = rt.rect;
            float size = axis == 0 ? r.width : r.height;

            if (size <= 0f)
                return false;

            row.MeasuredSize = size;
            float withPadding = size + row.Padding;

            if (measured[index])
                measuredTotal += withPadding - itemSize[index];
            else
            {
                measuredTotal += withPadding;
                measuredCount++;
                measured[index] = true;
            }

            if (!Mathf.Approximately(withPadding, itemSize[index]))
            {
                itemSize[index] = withPadding;
                MarkOffsetsDirtyFrom(index);
            }

            return reseedAllowed && ReseedUnmeasured();
        }

        // Rows that have never been laid out are worth more as the average of the rows
        // that have than as the prefab's smallest hint.
        private bool ReseedUnmeasured()
        {
            if (measuredCount == 0)
                return false;

            float average = measuredTotal / measuredCount;

            if (Mathf.Abs(average - seedEstimate) <= seedEstimate * RESEED_BAND)
                return false;

            seedEstimate = average;
            var firstChanged = -1;

            for (int i = 0; i < count; i++)
            {
                if (measured[i])
                    continue;

                itemSize[i] = average;

                if (firstChanged < 0)
                    firstChanged = i;
            }

            if (firstChanged < 0)
                return false;

            MarkOffsetsDirtyFrom(firstChanged);
            return true;
        }

        private void PositionShown()
        {
            for (int i = 0; i < shownOrdered.Count; i++)
            {
                LoopListViewItem2 row = shownOrdered[i];

                if (row == null)
                    continue;

                var rt = row.transform as RectTransform;

                if (rt == null)
                    continue;

                Place(rt, LeadingOf(row.ItemIndex));

                // Pooled rows come back in arbitrary sibling order; ascending order is
                // what makes overlapping rows stack the way the data reads.
                if (rt.GetSiblingIndex() != i)
                    rt.SetSiblingIndex(i);
            }
        }

        // Offsets the row so its leading edge sits `leadingOffset` px from the
        // Content's leading edge, deriving the anchored position from the row's own
        // anchors and pivot rather than replacing them.
        private void Place(RectTransform rt, float leadingOffset)
        {
            if (rt == null || content == null)
                return;

            int axis = MajorAxis;
            bool fromMax = mArrangeType == ListItemArrangeType.TopToBottom
                           || mArrangeType == ListItemArrangeType.RightToLeft;

            ScrollSpace.SetAnchoredComponent(rt, axis,
                ScrollSpace.AnchoredForEdge(rt, content.rect, axis, leadingOffset, fromMax));
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
                if (shown.TryGetValue(orderScratch[i], out LoopListViewItem2 row) && row != null)
                    shownOrdered.Add(row);
            }
        }

        private void RecycleOutside(int first, int last)
        {
            recycleScratch.Clear();

            foreach (KeyValuePair<int, LoopListViewItem2> entry in shown)
            {
                if (entry.Key < first || entry.Key > last)
                    recycleScratch.Add(entry.Key);
            }

            for (int i = 0; i < recycleScratch.Count; i++)
            {
                int index = recycleScratch[i];

                if (shown.TryGetValue(index, out LoopListViewItem2 row))
                {
                    shown.Remove(index);
                    ReturnToPool(row);
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
            foreach (KeyValuePair<int, LoopListViewItem2> entry in shown)
                ReturnToPool(entry.Value);

            shown.Clear();
            shownOrdered.Clear();
            knownNull.Clear();
            lastFirst = -1;
            lastLast = -2;
        }

        private LoopListViewItem2 RentFromPool(string prefabName)
        {
            if (!pools.TryGetValue(prefabName, out Stack<LoopListViewItem2> stack))
                return null;

            while (stack.Count > 0)
            {
                LoopListViewItem2 row = stack.Pop();

                if (row != null)
                    return row;
            }

            return null;
        }

        private void ReturnToPool(LoopListViewItem2 row)
        {
            if (row == null)
                return;

            string key = row.ItemPrefabName ?? string.Empty;

            if (!pools.TryGetValue(key, out Stack<LoopListViewItem2> stack))
            {
                stack = new Stack<LoopListViewItem2>();
                pools[key] = stack;
            }

            row.gameObject.SetActive(false);
            stack.Push(row);
        }

        // --- snapping ------------------------------------------------------

        private void CancelSnap()
        {
            snapArmed = false;
            snap.Cancel();
        }

        /// <summary>
        /// Aligns the nearest row's snap pivot with the viewport's snap pivot, both
        /// resolved to a fraction of the respective extent from the leading side by
        /// <see cref="ScrollSpace.SnapFactor"/>.
        /// </summary>
        private void BeginSnap()
        {
            if (count <= 0 || content == null || scrollRect == null)
                return;

            float viewport = ViewportMajorSize();

            if (viewport <= 0f)
                return;

            float viewportFactor = ScrollSpace.SnapFactor(mArrangeType, mViewPortSnapPivot);
            float itemFactor = ScrollSpace.SnapFactor(mArrangeType, mItemSnapPivot);
            float leading = ScrollLeading();
            float reference = leading + (viewportFactor * viewport);

            int candidate = IndexAtOffset(reference, true);
            float best = float.MaxValue;
            var bestIndex = -1;

            for (int i = Mathf.Max(0, candidate - 1); i <= Mathf.Min(count - 1, candidate + 1); i++)
            {
                float distance = Mathf.Abs(offset[i] + (itemFactor * itemSize[i]) - reference);

                if (distance >= best)
                    continue;

                best = distance;
                bestIndex = i;
            }

            if (bestIndex < 0)
                return;

            float maxScroll = Mathf.Max(0f, ContentExtent - viewport);
            float target = Mathf.Clamp(
                offset[bestIndex] + (itemFactor * itemSize[bestIndex]) - (viewportFactor * viewport),
                0f, maxScroll);

            if (Mathf.Abs(target - leading) < 1f)
                return;

            snap.Begin(leading, target);
            scrollRect.velocity = Vector2.zero;
        }
    }
}
