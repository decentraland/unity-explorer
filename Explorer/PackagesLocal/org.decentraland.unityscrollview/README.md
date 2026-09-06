# Decentraland Scroll View (org.decentraland.unityscrollview)

A clean-room virtualized list and grid for uGUI. It keeps the serialized field
names and the public API surface the Explorer's prefabs and view scripts were
authored against, so prefab YAML binds and round-trips unchanged.

Two components:

- `LoopListView2` — single axis, **variable** item extents (measured per row).
- `LoopGridView` — fixed cells across a minor axis, **uniform** cell size.

Both drive a `ScrollRect`'s Content directly. No `LayoutGroup` and no
`ContentSizeFitter` participate in the loop.

## Layout contract

Layout writes exactly two things:

1. `anchoredPosition` on each resident item — and only the component for the
   axis being arranged (the grid writes both, one per axis).
2. The Content's size on the arrange axis.

It never writes `anchorMin`, `anchorMax` or `pivot` on an item or on an authored
Content, and never writes an item's `sizeDelta` unless a grid's `mItemSize`
explicitly specifies that component. Under Unity's rect model the same
`sizeDelta` means a *size* when `anchorMin == anchorMax` on an axis and a *delta
from the anchor span* when they differ, so imposing anchors on an authored item
silently resizes it. `Runtime/ScrollSpace.cs` carries the full coordinate model,
including the single "leading space" scroll coordinate the four
`ListItemArrangeType` directions share.

Consequences worth knowing when authoring a prefab:

- An item's cross-axis placement and size are whatever the prefab says. Center
  it, pin it to a corner, stretch it — layout will not touch it.
- An item stretched **along the arrange axis** is not measurable (its extent is
  a function of the Content being sized from it), so a list leaves such a row on
  the running estimate and a grid leaves its authored size alone.
- A Content stretched on the arrange axis is supported: its size is written as
  the `sizeDelta` that yields the required extent, not as the extent itself.
- A `ScrollRect` and a Content that already exist on the prefab keep their
  authored settings, including which axes may scroll. Only ones these components
  have to invent get defaults derived from `mArrangeType`.

## Item extents (`LoopListView2`)

Each index starts on an estimate and is replaced by the row's real laid-out
extent the first time it materializes. The estimate is the *smallest* registered
prefab's extent, refined towards the running average of the rows measured so
far; an estimate below the truth over-fills the window and every extra row is
then measured and corrected, while one above the truth would leave the tail of
the viewport empty with nothing scheduled to fill it.

Changing the item count keeps every extent already measured and seeds only the
indices that did not exist before, which is what lets the 20-plus call sites
that pass `resetPos: false` hold their scroll position across an append.

When re-seeding shifts the offsets under the viewport, the scroll position is
shifted with them so the row at the top of the window does not move on screen.

## Serialized fields

`ItemPrefabConfData` — `mItemPrefab`, `mPadding`, `mInitCreateCount`,
`mStartPosOffset`, `mItemSize`. All are honoured. `mStartPosOffset` is read from
the list's **first** entry, as the inset belongs to the list rather than to a
kind of row. `mItemSize` is an optional pre-measurement hint; when it is 0 the
prefab's own rect is used.

`LoopListView2` — `mItemPrefabDataList`, `mArrangeType`, `mSupportScrollBar`,
`mItemSnapEnable`, `mViewPortSnapPivot`, `mItemSnapPivot`. All are honoured.
`mSupportScrollBar` hides the `ScrollRect`'s scrollbars when false. Snapping is
armed by the end of a drag and fires once the `ScrollRect`'s inertia is spent, so
a flick still flings; it then aligns the nearest row's `mItemSnapPivot` with the
viewport's `mViewPortSnapPivot` over a short eased tween, and any new drag or
programmatic move cancels it.

`LoopGridView` — `mItemPrefabDataList`, `mArrangeType`,
`mFixedRowOrColumnCount`, `mPadding`, `mItemPadding`, `mItemSize`,
`mGridFixedType`, `mItemRecycleDistance`, `mItemSnapEnable`,
`mViewPortSnapPivot`, `mItemSnapPivot`. All are honoured. `mGridFixedType` 0
takes the cells-across count from `mFixedRowOrColumnCount`; 1 derives it from
the space the viewport offers. `mItemRecycleDistance` is how far past either
viewport edge, along the scrolling axis, a line stays resident before its cells
go back to the pool; the component read is the scrolling axis's one, and 0
keeps exactly the lines that overlap the viewport. Snapping arms, rests and
tweens as on the list, aligning the nearest line's cells.

Snap pivots follow `RectTransform.pivot`'s bottom-left-origin convention: the
arrange-axis component names a fraction of the item's (or viewport's) extent
measured from the max edge for `TopToBottom`/`RightToLeft` and from the min edge
otherwise, so the shipped `(0, 0)` on a top-to-bottom list aligns a row's bottom
edge with the viewport's bottom edge.

## Programmatic moves (`LoopListView2`)

`MovePanelToItemIndex` aims from the extents known at the time of the call, then
re-aims from the extents measured by the rows it just materialized, for a small
bounded number of passes. A jump across rows that were still on estimates
therefore lands the row exactly where it was asked to be, not off by their
measurement error.

`MovePanelByOffset` moves the Content by the given delta in the Content's own
anchored-position space (+Y for a vertical list, +X for a horizontal one),
whichever way the list runs. A list that grows at its leading end keeps the rows
already on screen stationary by passing the negated extent of the inserted rows.

## Prefab templates inside the view

An `mItemPrefab` may point at a GameObject living inside the view's own
hierarchy rather than at a prefab asset. Such a template is a real, active
object; each view deactivates the ones under itself once at init so the template
does not draw as a stray row on top of the list.

## Tests

`Tests/` is an EditMode assembly (`SuperScrollView.Tests`, listed under
`testables` in the Explorer manifest). A view initializes on its first public
call, so the suite runs without the player loop: it drives scrolling through the
`ScrollRect` event the views listen to, and per-frame work through each view's
internal `Tick`.
