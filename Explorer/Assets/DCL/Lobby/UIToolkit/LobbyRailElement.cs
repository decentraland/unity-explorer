using System;
using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby
{
    /// <summary>
    ///     A horizontal strip of cards that pages by the mouse wheel or by
    ///     the arrows shown while it is hovered, and scrolls freely by dragging. Releasing a drag lets the strip run on with the fling and
    ///     settles it on the nearest card; the arrows page on from that card and one dot per page tracks the nearest page.
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

        // Seconds of its release speed a flung drag runs on for, capped at a page
        private const float FLING_PROJECTION = 0.12f;

        // A pointer that rested longer than this before its release was not flung
        private const long FLING_STALE_MS = 100;

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

        // The card at the left edge of the viewport since the last snap; the arrows page on from it
        private int currentCard;

        private int pressedPointerId = PointerId.invalidPointerId;
        private float pressX;
        private float pressOffset;
        private bool dragging;

        // Speed of the pointer along the drag in panel units per second, from its last two moves
        private float velocity;
        private float lastMoveX;
        private long lastMoveTime;

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

        /// <summary>
        ///     Makes the arrows slide a single card instead of a whole page; the wheel and the dots keep going by pages.
        /// </summary>
        [UxmlAttribute]
        public bool ArrowsMoveOneCard { get; set; }

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
        ///     the rail slides back to the start of its page, or to the last one the cards still fill.
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
                currentCard = 0;
                SelectPage(0);
            }
            else
                SnapTo(CurrentPage);
        }

        /// <summary>
        ///     Slides to the first card of <paramref name="page" />, clamped to the pages the shown cards fill.
        /// </summary>
        public void SnapTo(int page)
        {
            page = Mathf.Clamp(page, 0, Mathf.Max(0, PageCount - 1));
            SnapToCard(page * cardsPerPage, page);
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

        private void OnPreviousClicked()
        {
            if (ArrowsMoveOneCard)
                StepToCard(currentCard - 1);
            else
                SnapTo(PageBefore());
        }

        private void OnNextClicked()
        {
            if (ArrowsMoveOneCard)
                StepToCard(currentCard + 1);
            else
                SnapTo(PageAfter());
        }

        // The last cards share the offset of the end: stepping into them lands on the first of those, so one step back leaves the end
        private void StepToCard(int card)
        {
            card = CardAt(CardOffset(Mathf.Clamp(card, 0, Mathf.Max(0, shownCount - 1))));
            SnapToCard(card, PageAt(CardOffset(card)));
        }

        // Presses on the arrows are theirs: dragging from one would steal the click
        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            if (evt.target is VisualElement target && (previous.Contains(target) || next.Contains(target))) return;

            // A press still tracked here is stale: its release went to an element the rail could not hear
            EndPress(0f);

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
                EndPress(0f);
                return;
            }

            if (!dragging)
            {
                if (Mathf.Abs(evt.position.x - pressX) < DRAG_THRESHOLD) return;

                dragging = true;
                AddToClassList(USS_INSTANT);
                viewport.CapturePointer(evt.pointerId);

                // Re-based on the pointer and on where the strip is drawn (mid-snap included), so the strip follows from there
                // rather than jumping by the threshold or to the end of a snap still under way
                pressX = evt.position.x;
                pressOffset = -content.resolvedStyle.translate.x;
                velocity = 0f;
                lastMoveX = pressX;
                lastMoveTime = evt.timestamp;
            }

            long elapsed = evt.timestamp - lastMoveTime;

            if (elapsed > 0)
            {
                velocity = (evt.position.x - lastMoveX) * 1000f / elapsed;
                lastMoveX = evt.position.x;
                lastMoveTime = evt.timestamp;
            }

            ApplyOffset(Mathf.Clamp(pressOffset - (evt.position.x - pressX), 0f, MaxOffset()));
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != pressedPointerId) return;

            // A pointer that rested before letting go was not flung
            EndPress(evt.timestamp - lastMoveTime > FLING_STALE_MS ? 0f : velocity);
        }

        // The capture can be taken away mid-drag (by the system or another element); the rail must not stay stuck between cards.
        // The capture-out of a card the rail takes the pointer from bubbles through here too: only the viewport's own counts
        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.target != viewport || !dragging) return;

            EndPress(0f);
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
        ///     Forgets the tracked press and, when it had turned into a drag, gives the pointer back and settles the strip on a card:
        ///     the nearest one to where it was dropped, carried on by <paramref name="flingVelocity" /> for up to a page.
        /// </summary>
        private void EndPress(float flingVelocity)
        {
            int pointerId = pressedPointerId;
            pressedPointerId = PointerId.invalidPointerId;
            StopListeningToCaptor();

            if (!dragging) return;

            // Only releases while the viewport still holds the pointer, so a capture already taken away is left alone
            viewport.ReleasePointer(pointerId);
            EndDrag(flingVelocity);
        }

        private void EndDrag(float flingVelocity)
        {
            dragging = false;
            RemoveFromClassList(USS_INSTANT);

            float pageWidth = cardsPerPage * CardStride();
            float fling = Mathf.Clamp(-flingVelocity * FLING_PROJECTION, -pageWidth, pageWidth);
            int card = CardAt(offset + fling);
            SnapToCard(card, PageAt(CardOffset(card)));
        }

        private void OnWheel(WheelEvent evt)
        {
            evt.StopPropagation();

            if (UnityEngine.Time.unscaledTime - lastWheelTime < WHEEL_COOLDOWN) return;

            lastWheelTime = UnityEngine.Time.unscaledTime;
            SnapTo(evt.delta.y > 0f ? PageAfter() : PageBefore());
        }

        // The strides are only known once the cards are laid out, so the card the rail is on is re-placed then
        private void OnContentGeometryChanged(GeometryChangedEvent _)
        {
            if (dragging) return;

            ApplyOffset(CardOffset(currentCard));
        }

        private void SnapToCard(int card, int page)
        {
            currentCard = card;
            ApplyOffset(CardOffset(card));
            SelectPage(page);
        }

        // From a card inside a page, the arrows go to the pages starting after and before it
        private int PageAfter() =>
            currentCard / cardsPerPage + 1;

        private int PageBefore() =>
            Mathf.Max(0, currentCard - 1) / cardsPerPage;

        /// <summary>
        ///     Distance from one card's left edge to the next one's, spacing included. A single card cannot page, so its stride does not matter.
        /// </summary>
        private float CardStride() =>
            content.childCount > 1 ? content[1].layout.x - content[0].layout.x : 0f;

        // Measured against the content box of the viewport: its padding, when it has any, is room for the shadows of the cards at its edges
        private float MaxOffset() =>
            Mathf.Max(0f, content.layout.width - viewport.contentRect.width);

        // The content cannot slide past its end, so the last cards start wherever the content ends rather than a full stride further in
        private float CardOffset(int card) =>
            Mathf.Min(card * CardStride(), MaxOffset());

        private float PageOffset(int page) =>
            CardOffset(page * cardsPerPage);

        // Among the cards sharing the offset of the end, the first is the one the strip is on there
        private int CardAt(float at) =>
            Nearest(at, shownCount, CardStride());

        private int PageAt(float at) =>
            Nearest(at, PageCount, cardsPerPage * CardStride());

        private int Nearest(float at, int count, float step)
        {
            float max = MaxOffset();
            var nearest = 0;
            float nearestDistance = float.MaxValue;

            for (var i = 0; i < count; i++)
            {
                float distance = Mathf.Abs(at - Mathf.Min(i * step, max));
                if (distance >= nearestDistance) continue;

                nearestDistance = distance;
                nearest = i;
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
            previous.SetDisplayed(currentCard > 0);

            // A drag can settle nearest to the last page while the strip has not reached its end: the arrow stays until it has
            next.SetDisplayed(page < PageCount - 1 || CardOffset(currentCard) < MaxOffset() - 0.5f);

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
