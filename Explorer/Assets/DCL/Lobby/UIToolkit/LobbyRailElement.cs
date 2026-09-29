using System;
using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby
{
    /// <summary>
    ///     UI Toolkit counterpart of <see cref="LobbyPagedRailView" />: a horizontal strip of cards that pages by dragging, by the mouse
    ///     wheel or by the arrows shown while it is hovered. Releasing a drag snaps to the nearest page and one dot per page tracks the position.
    ///     The cards are its children, added by the owner; the chrome (viewport, arrows and dots) is built here and styled by LobbyRail.uss,
    ///     which also owns the snap: the content slides through a USS transition of its translate.
    /// </summary>
    [UxmlElement]
    public partial class LobbyRailElement : VisualElement
    {
        private const string USS_BLOCK = "lobby-rail";
        private const string USS_INSTANT = USS_BLOCK + "--instant";
        private const string USS_TRACK = USS_BLOCK + "__track";
        private const string USS_VIEWPORT = USS_BLOCK + "__viewport";
        private const string USS_CONTENT = USS_BLOCK + "__content";
        private const string USS_ARROW = USS_BLOCK + "__arrow";
        private const string USS_ARROW_PREVIOUS = USS_ARROW + "--previous";
        private const string USS_ARROW_NEXT = USS_ARROW + "--next";
        private const string USS_ARROW_ICON = USS_BLOCK + "__arrow-icon";
        private const string USS_DOTS = USS_BLOCK + "__dots";
        private const string USS_DOT = USS_BLOCK + "__dot";
        private const string USS_DOT_SELECTED = USS_DOT + "--selected";
        private const string USS_CARD = USS_BLOCK + "__card";

        private const string PREVIOUS_NAME = "Previous";
        private const string NEXT_NAME = "Next";

        // Pointer travel before a press on a card turns into a drag of the rail
        private const float DRAG_THRESHOLD = 8f;

        // Bit of PointerEventBase.pressedButtons that stands for the left mouse button
        private const int LEFT_BUTTON_MASK = 1;

        // Matches the snap duration of the stylesheet: wheel events arriving faster than that are dropped
        private const float WHEEL_COOLDOWN = 0.25f;

        private readonly VisualElement viewport;
        private readonly VisualElement content;
        private readonly Button previous;
        private readonly Button next;
        private readonly VisualElement dots;
        private readonly Action endInstantMove;

        private int cardsPerPage = 3;
        private int shownCount;
        private float offset;
        private float lastWheelTime;

        private int pressedPointerId = PointerId.invalidPointerId;
        private float pressX;
        private float pressOffset;
        private bool dragging;

        // The descendant holding the pointer capture of the press, hearing the moves and the release on the rail's behalf
        private VisualElement? pressedCaptor;

        /// <summary>
        ///     Cards a page advances by; a page is this many card strides wide, whatever the viewport shows.
        /// </summary>
        [UxmlAttribute]
        public int CardsPerPage
        {
            get => cardsPerPage;
            set => cardsPerPage = Mathf.Max(1, value);
        }

        public int CurrentPage { get; private set; }

        public int PageCount => (shownCount + cardsPerPage - 1) / cardsPerPage;

        /// <summary>
        ///     Cards added to the rail land in the content strip, not next to the chrome.
        /// </summary>
        public override VisualElement contentContainer => content;

        public LobbyRailElement()
        {
            AddToClassList(USS_BLOCK);

            // The track holds the viewport and, outside it, the arrows: the viewport clips its cards, the track lets the arrows hang beside them
            var track = new VisualElement { name = "Track" };
            track.AddToClassList(USS_TRACK);
            hierarchy.Add(track);

            viewport = new VisualElement { name = "Viewport", pickingMode = PickingMode.Position };
            viewport.AddToClassList(USS_VIEWPORT);
            track.Add(viewport);

            content = new VisualElement { name = "Content", pickingMode = PickingMode.Ignore };
            content.AddToClassList(USS_CONTENT);
            viewport.Add(content);

            previous = CreateArrow(PREVIOUS_NAME, USS_ARROW_PREVIOUS);
            next = CreateArrow(NEXT_NAME, USS_ARROW_NEXT);
            track.Add(previous);
            track.Add(next);

            dots = new VisualElement { name = "Dots", pickingMode = PickingMode.Ignore };
            dots.AddToClassList(USS_DOTS);
            hierarchy.Add(dots);

            endInstantMove = EndInstantMove;

            previous.clicked += OnPreviousClicked;
            next.clicked += OnNextClicked;

            // Trickle down: the press is seen before the pressed card captures the pointer and stops the event
            RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);

            // Once a descendant captures the pointer, the panel delivers the pointer events to that element alone and skips every
            // ancestor: the moves and the release are heard through the captor itself (see OnPointerCapture) and through the viewport,
            // which is the target while nothing captures and once the rail takes the pointer over
            RegisterCallback<PointerCaptureEvent>(OnPointerCapture);
            viewport.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            viewport.RegisterCallback<PointerUpEvent>(OnPointerUp);
            viewport.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            RegisterCallback<WheelEvent>(OnWheel);
            content.RegisterCallback<GeometryChangedEvent>(OnContentGeometryChanged);

            SelectPage(0);
        }

        /// <summary>
        ///     Rebuilds the dots for <paramref name="count" /> shown cards. Rewinding jumps to the first page without sliding; otherwise
        ///     the rail stays on its page, or slides to the last one the cards still fill.
        /// </summary>
        public void SetCardCount(int count, bool rewind = true)
        {
            shownCount = count;

            // The stylesheet spaces the cards through this class: a wildcard child selector on the strip left them touching
            for (var i = 0; i < content.childCount; i++)
                content[i].AddToClassList(USS_CARD);

            ShowDots(PageCount);

            if (rewind)
            {
                MoveInstantly(0f);
                SelectPage(0);
            }
            else
                SnapTo(CurrentPage);
        }

        /// <summary>
        ///     Slides to <paramref name="page" />, clamped to the pages the shown cards fill.
        /// </summary>
        public void SnapTo(int page)
        {
            page = Mathf.Clamp(page, 0, Mathf.Max(0, PageCount - 1));
            ApplyOffset(PageOffset(page));
            SelectPage(page);
        }

        private static Button CreateArrow(string name, string modifier)
        {
            var arrow = new Button { name = name };
            arrow.AddToClassList(USS_ARROW);
            arrow.AddToClassList(modifier);

            var icon = new VisualElement { name = "Icon", pickingMode = PickingMode.Ignore };
            icon.AddToClassList(USS_ARROW_ICON);
            arrow.Add(icon);

            return arrow;
        }

        private void OnPreviousClicked() =>
            SnapTo(CurrentPage - 1);

        private void OnNextClicked() =>
            SnapTo(CurrentPage + 1);

        // Presses on the arrows are theirs: dragging from one would steal the click
        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            if (evt.target is VisualElement target && (previous.Contains(target) || next.Contains(target))) return;

            // A press still tracked here is stale: its release went to an element the rail could not hear
            EndPress();

            pressedPointerId = evt.pointerId;
            pressX = evt.position.x;
            pressOffset = offset;
        }

        // The capture event bubbles up from the captor, the only element the pressed pointer's events reach from now on
        private void OnPointerCapture(PointerCaptureEvent evt)
        {
            if (evt.pointerId != pressedPointerId || evt.target is not VisualElement captor || captor == viewport || captor == pressedCaptor) return;

            ListenToCaptor(captor);
        }

        // Past the threshold the rail takes the pointer over: the pressed card loses its capture and never reports the click
        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != pressedPointerId) return;

            // The button is up: the release was delivered to an element outside the rail's hearing, so the press is over
            if ((evt.pressedButtons & LEFT_BUTTON_MASK) == 0)
            {
                EndPress();
                return;
            }

            float delta = evt.position.x - pressX;

            if (!dragging)
            {
                if (Mathf.Abs(delta) < DRAG_THRESHOLD) return;

                dragging = true;
                AddToClassList(USS_INSTANT);
                viewport.CapturePointer(evt.pointerId);
            }

            ApplyOffset(Mathf.Clamp(pressOffset - delta, 0f, MaxOffset()));
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != pressedPointerId) return;

            EndPress();
        }

        // The capture can be taken away mid-drag (by the system or another element); the rail must not stay stuck between pages.
        // The capture-out of a card the rail takes the pointer from bubbles through here too: only the viewport's own counts
        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.target != viewport || !dragging) return;

            EndPress();
        }

        private void ListenToCaptor(VisualElement captor)
        {
            StopListeningToCaptor();
            pressedCaptor = captor;
            captor.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            captor.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        private void StopListeningToCaptor()
        {
            if (pressedCaptor == null) return;

            pressedCaptor.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            pressedCaptor.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            pressedCaptor = null;
        }

        /// <summary>
        ///     Forgets the tracked press and, when it had turned into a drag, gives the pointer back and snaps to the nearest page.
        /// </summary>
        private void EndPress()
        {
            int pointerId = pressedPointerId;
            pressedPointerId = PointerId.invalidPointerId;
            StopListeningToCaptor();

            if (!dragging) return;

            // Only releases while the viewport still holds the pointer, so a capture already taken away is left alone
            viewport.ReleasePointer(pointerId);
            EndDrag();
        }

        private void EndDrag()
        {
            dragging = false;
            RemoveFromClassList(USS_INSTANT);
            SnapTo(PageAt(offset));
        }

        private void OnWheel(WheelEvent evt)
        {
            evt.StopPropagation();

            if (UnityEngine.Time.unscaledTime - lastWheelTime < WHEEL_COOLDOWN) return;

            lastWheelTime = UnityEngine.Time.unscaledTime;
            SnapTo(CurrentPage + (evt.delta.y > 0f ? 1 : -1));
        }

        // The strides are only known once the cards are laid out, so the page the rail is on is re-placed then
        private void OnContentGeometryChanged(GeometryChangedEvent _)
        {
            if (dragging) return;

            ApplyOffset(PageOffset(CurrentPage));
        }

        /// <summary>
        ///     Distance from one card's left edge to the next one's, spacing included. A single card cannot page, so its stride does not matter.
        /// </summary>
        private float CardStride() =>
            content.childCount > 1 ? content[1].layout.x - content[0].layout.x : 0f;

        private float MaxOffset() =>
            Mathf.Max(0f, content.layout.width - viewport.layout.width);

        // The content cannot slide past its end, so the last page starts wherever the content ends rather than a full page in
        private float PageOffset(int page) =>
            Mathf.Min(page * cardsPerPage * CardStride(), MaxOffset());

        private int PageAt(float at)
        {
            var nearest = 0;
            float nearestDistance = float.MaxValue;

            for (var page = 0; page < PageCount; page++)
            {
                float distance = Mathf.Abs(at - PageOffset(page));
                if (distance >= nearestDistance) continue;

                nearestDistance = distance;
                nearest = page;
            }

            return nearest;
        }

        private void ApplyOffset(float value)
        {
            offset = value;
            content.style.translate = new Translate(new Length(-value), new Length(0f));
        }

        // The transition is switched off for the frame the translate changes in, and back on once that change has been resolved
        private void MoveInstantly(float value)
        {
            AddToClassList(USS_INSTANT);
            ApplyOffset(value);
            schedule.Execute(endInstantMove);
        }

        private void EndInstantMove()
        {
            if (!dragging)
                RemoveFromClassList(USS_INSTANT);
        }

        private void SelectPage(int page)
        {
            CurrentPage = page;
            previous.SetDisplayed(page > 0);
            next.SetDisplayed(page < PageCount - 1);

            for (var i = 0; i < dots.childCount; i++)
                dots[i].EnableInClassList(USS_DOT_SELECTED, i == page);
        }

        // A single page needs no navigation hint
        private void ShowDots(int pageCount)
        {
            int visibleDots = pageCount > 1 ? pageCount : 0;

            for (int i = dots.childCount; i < visibleDots; i++)
            {
                var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                dot.AddToClassList(USS_DOT);
                dots.Add(dot);
            }

            for (var i = 0; i < dots.childCount; i++)
                dots[i].SetDisplayed(i < visibleDots);
        }
    }
}
