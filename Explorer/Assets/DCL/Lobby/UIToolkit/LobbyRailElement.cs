using System;
using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby
{
    /// <summary>
    ///     A horizontal strip of cards that pages by the mouse wheel, the arrows or the dots, and scrolls freely by
    ///     dragging; a released drag settles on the nearest card. The arrows wrap round past either end; the wheel stops
    ///     there and each dot goes to its page. The cards are its children; the chrome is built here and styled by
    ///     LobbyRail.uss, which also owns the snap through a USS transition of the translate.
    /// </summary>
    [UxmlElement]
    public partial class LobbyRailElement : VisualElement
    {
        private const string USS_BLOCK = "lobby-rail";
        private const string USS_INSTANT = USS_BLOCK + "--instant";
        private const string USS_VIEWPORT = USS_BLOCK + "__viewport";
        private const string USS_CONTENT = USS_BLOCK + "__content";
        private const string USS_PAGER = USS_BLOCK + "__pager";
        private const string USS_PAGER_PILL = USS_BLOCK + "__pager-pill";
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

        // Seconds of its release speed a flung drag runs on for, capped at a page
        private const float FLING_PROJECTION = 0.12f;

        // A pointer that rested longer than this before its release was not flung
        private const long FLING_STALE_MS = 100;

        // Matches the snap duration of the stylesheet
        private const float WHEEL_COOLDOWN = 0.25f;

        private const float MS_PER_SECOND = 1000f;

        // Slack under which the strip counts as having reached its end
        private const float END_TOLERANCE = 0.5f;

        // How far the layout's rounding of every edge to the pixel grid can put the measured end of the strip off a card
        private const float PIXEL_ROUNDING = 4f;

        /// <summary>Cards per page until the UXML sets cards-per-page.</summary>
        private const int DEFAULT_CARDS_PER_PAGE = 3;

        private readonly VisualElement viewport;
        private readonly VisualElement content;
        private readonly VisualElement pill;
        private readonly Button previous;
        private readonly Button next;
        private readonly VisualElement dots;
        private readonly Action endInstantMove;

        private int cardsPerPage = DEFAULT_CARDS_PER_PAGE;
        private int shownCount;
        private float offset;
        private float lastWheelTime;

        // The card at the left edge of the viewport since the last snap
        private int currentCard;

        private int pressedPointerId = PointerId.invalidPointerId;
        private float pressX;
        private float pressOffset;
        private bool dragging;

        // Panel units per second, from the last two moves of the drag
        private float velocity;
        private float lastMoveX;
        private long lastMoveTime;

        // The descendant holding the pointer capture of the press
        private VisualElement? pressedCaptor;

        /// <summary>Cards a page advances by, whatever the viewport shows.</summary>
        [UxmlAttribute]
        public int CardsPerPage
        {
            get => cardsPerPage;
            set => cardsPerPage = Mathf.Max(1, value);
        }

        /// <summary>The arrows slide one card instead of a page; the wheel and the dots keep paging.</summary>
        [UxmlAttribute]
        public bool ArrowsMoveOneCard { get; set; }

        public int CurrentPage { get; private set; }

        public int PageCount => (shownCount + cardsPerPage - 1) / cardsPerPage;

        public override VisualElement contentContainer => content;

        public LobbyRailElement()
        {
            AddToClassList(USS_BLOCK);

            // The viewport clips the cards
            viewport = new VisualElement { name = "Viewport", pickingMode = PickingMode.Position };
            viewport.AddToClassList(USS_VIEWPORT);
            hierarchy.Add(viewport);

            content = new VisualElement { name = "Content", pickingMode = PickingMode.Ignore };
            content.AddToClassList(USS_CONTENT);
            viewport.Add(content);

            // The pager row keeps the height of the dots; the pill hangs centred over it with the arrows around the dots
            var pager = new VisualElement { name = "Pager", pickingMode = PickingMode.Ignore };
            pager.AddToClassList(USS_PAGER);
            hierarchy.Add(pager);

            pill = new VisualElement { name = "Pill", pickingMode = PickingMode.Ignore };
            pill.AddToClassList(USS_PAGER_PILL);
            pager.Add(pill);

            previous = CreateArrow(PREVIOUS_NAME, USS_ARROW_PREVIOUS);
            next = CreateArrow(NEXT_NAME, USS_ARROW_NEXT);

            dots = new VisualElement { name = "Dots", pickingMode = PickingMode.Ignore };
            dots.AddToClassList(USS_DOTS);

            pill.Add(previous);
            pill.Add(dots);
            pill.Add(next);

            endInstantMove = EndInstantMove;

            previous.clicked += OnPreviousClicked;
            next.clicked += OnNextClicked;

            // Trickle down: the press is seen before the pressed card captures the pointer and stops the event
            RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);

            // A capturing descendant alone receives the pointer events, so moves and releases are heard through it
            // (see OnPointerCapture) and through the viewport, the target without a capture or once the rail takes over
            RegisterCallback<PointerCaptureEvent>(OnPointerCapture);
            viewport.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            viewport.RegisterCallback<PointerUpEvent>(OnPointerUp);
            viewport.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            RegisterCallback<WheelEvent>(OnWheel);
            content.RegisterCallback<GeometryChangedEvent>(OnContentGeometryChanged);

            SelectPage(0);
        }

        /// <summary>
        ///     Rebuilds the dots for <paramref name="count" /> shown cards. Rewinding jumps to the first page without
        ///     sliding; otherwise the rail slides back to the start of its page, or to the last one the cards fill.
        /// </summary>
        public void SetCardCount(int count, bool rewind = true)
        {
            shownCount = count;

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

        /// <summary>Slides to the first card of <paramref name="page" />, clamped to the filled pages.</summary>
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

        // From the start the rail wraps round to the last page
        private void OnPreviousClicked()
        {
            if (ArrowsMoveOneCard)
                StepToCard(currentCard - 1);
            else
                SnapTo(currentCard == 0 ? PageCount - 1 : PageBefore());
        }

        // Past the last page the rail wraps round to the first, once the strip has reached its end
        private void OnNextClicked()
        {
            if (ArrowsMoveOneCard)
                StepToCard(currentCard + 1);
            else
            {
                int page = PageAfter();
                SnapTo(page < PageCount || !AtEnd() ? page : 0);
            }
        }

        // The last cards share the offset of the end, so stepping into them lands on the first of those; a step past
        // either end wraps round
        private void StepToCard(int card)
        {
            if (card < 0)
                card = shownCount - 1;
            else if (card >= shownCount || (card > currentCard && AtEnd()))
                card = 0;

            card = CardAt(CardOffset(card));
            SnapToCard(card, PageAt(CardOffset(card)));
        }

        // Presses on the arrows and the dots are theirs: dragging from one would steal the click
        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            if (evt.target is VisualElement target && (previous.Contains(target) || next.Contains(target) || dots.Contains(target))) return;

            // A press still tracked here is stale: its release went to an element the rail could not hear
            EndPress(0f);

            pressedPointerId = evt.pointerId;
            pressX = evt.position.x;
            pressOffset = offset;
        }

        private void OnPointerCapture(PointerCaptureEvent evt)
        {
            if (evt.pointerId != pressedPointerId || evt.target is not VisualElement captor || captor == viewport || captor == pressedCaptor) return;

            ListenToCaptor(captor);
        }

        // Past the threshold the rail takes the pointer over: the pressed card loses its capture and its click
        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != pressedPointerId) return;

            // The button is up: the release went to an element outside the rail's hearing
            if ((evt.pressedButtons & ClickOrDragManipulator.LEFT_BUTTON_MASK) == 0)
            {
                EndPress(0f);
                return;
            }

            if (!dragging)
            {
                if (Mathf.Abs(evt.position.x - pressX) < ClickOrDragManipulator.DRAG_THRESHOLD) return;

                dragging = true;
                AddToClassList(USS_INSTANT);
                viewport.CapturePointer(evt.pointerId);

                // Re-based on where the strip is drawn, mid-snap included, so it follows from there rather than jumping
                pressX = evt.position.x;
                pressOffset = -content.resolvedStyle.translate.x;
                velocity = 0f;
                lastMoveX = pressX;
                lastMoveTime = evt.timestamp;
            }

            long elapsed = evt.timestamp - lastMoveTime;

            if (elapsed > 0)
            {
                velocity = (evt.position.x - lastMoveX) * MS_PER_SECOND / elapsed;
                lastMoveX = evt.position.x;
                lastMoveTime = evt.timestamp;
            }

            ApplyOffset(Mathf.Clamp(pressOffset - (evt.position.x - pressX), 0f, MaxOffset()));
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != pressedPointerId) return;

            EndPress(evt.timestamp - lastMoveTime > FLING_STALE_MS ? 0f : velocity);
        }

        // The capture can be taken away mid-drag; a card's capture-out after the rail took the pointer bubbles here too
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

        // Forgets the tracked press; a drag settles the strip on the nearest card the fling carries it to
        private void EndPress(float flingVelocity)
        {
            int pointerId = pressedPointerId;
            pressedPointerId = PointerId.invalidPointerId;
            StopListeningToCaptor();

            if (!dragging) return;

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

            // A horizontal-only scroll must not fall through as a page back
            if (Mathf.Approximately(evt.delta.y, 0f) || UnityEngine.Time.unscaledTime - lastWheelTime < WHEEL_COOLDOWN) return;

            lastWheelTime = UnityEngine.Time.unscaledTime;
            SnapTo(evt.delta.y > 0f ? PageAfter() : PageBefore());
        }

        // The strides are only known once the cards are laid out, so the current card is re-placed then
        private void OnContentGeometryChanged(GeometryChangedEvent _)
        {
            if (dragging) return;

            ApplyOffset(CardOffset(currentCard));
            SelectPage(CurrentPage);
        }

        private void SnapToCard(int card, int page)
        {
            currentCard = card;
            ApplyOffset(CardOffset(card));
            SelectPage(page);
        }

        private int PageAfter() =>
            (currentCard / cardsPerPage) + 1;

        private int PageBefore() =>
            Mathf.Max(0, currentCard - 1) / cardsPerPage;

        // Distance from one card's left edge to the next one's, spacing included
        private float CardStride() =>
            content.childCount > 1 ? content[1].layout.x - content[0].layout.x : 0f;

        // Laid out rather than a multiple of the stride, whose pixel rounding would multiply up with the card index
        private float CardX(int card) =>
            card < content.childCount ? content[card].layout.x : 0f;

        // The strip ends at the last shown card, not at the trailing margin the content runs on by; an end measured within
        // the pixel rounding of a card is put on that card, so it is not one more step past it
        private float MaxOffset()
        {
            int lastShown = Mathf.Min(shownCount, content.childCount) - 1;
            if (lastShown < 0) return 0f;

            float max = Mathf.Max(0f, content[lastShown].layout.xMax - viewport.contentRect.width);

            for (var i = 0; i <= lastShown; i++)
            {
                float x = CardX(i);
                if (Mathf.Abs(x - max) <= PIXEL_ROUNDING) return x;
            }

            return max;
        }

        // A drag can settle nearest to the last page while the strip has not reached its end; before the first layout
        // pass nothing is measured and the strip counts as at its end
        private bool AtEnd()
        {
            float maxOffset = MaxOffset();
            return float.IsNaN(maxOffset) || offset >= maxOffset - END_TOLERANCE;
        }

        // The content cannot slide past its end, so the last cards share the offset of the end
        private float CardOffset(int card) =>
            Mathf.Min(CardX(card), MaxOffset());

        private int CardAt(float at) =>
            Nearest(at, shownCount, 1);

        private int PageAt(float at) =>
            Nearest(at, PageCount, cardsPerPage);

        private int Nearest(float at, int count, int cardsPerStep)
        {
            float max = MaxOffset();
            var nearest = 0;
            float nearestDistance = float.MaxValue;

            for (var i = 0; i < count; i++)
            {
                float distance = Mathf.Abs(at - Mathf.Min(CardX(i * cardsPerStep), max));
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

        // The transition is switched off for the frame the translate changes in
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

            for (var i = 0; i < dots.childCount; i++)
                dots[i].EnableInClassList(USS_DOT_SELECTED, i == page);
        }

        // The pill goes with the dots: nothing to page through leaves no arrows either
        private void ShowDots(int pageCount)
        {
            int visibleDots = pageCount > 1 ? pageCount : 0;
            pill.SetDisplayed(visibleDots > 0);

            for (int i = dots.childCount; i < visibleDots; i++)
            {
                var dot = new VisualElement();
                dot.AddToClassList(USS_DOT);
                dot.AddToClassList(VisualElementsExtensions.INTERACTABLE_CLASS);
                dot.RegisterCallback<ClickEvent>(OnDotClicked);
                dots.Add(dot);
            }

            for (var i = 0; i < dots.childCount; i++)
                dots[i].SetDisplayed(i < visibleDots);
        }

        // The dot stands at the index of its page
        private void OnDotClicked(ClickEvent evt) =>
            SnapTo(dots.IndexOf((VisualElement)evt.currentTarget));
    }
}
