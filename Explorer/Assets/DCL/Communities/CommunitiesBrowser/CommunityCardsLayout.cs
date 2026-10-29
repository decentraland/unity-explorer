using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesBrowser
{
    /// <summary>
    ///     Card rows of the browser: authored for the full-width explore panel, and re-flowed for the explore section modal,
    ///     whose right section (400px of the host taken by the left column) fits one card fewer per row only once the cards shrink.
    /// </summary>
    public static class CommunityCardsLayout
    {
        public const int COLUMNS = 5;
        public const int COMPACT_COLUMNS = 4;
        public const float CARD_WIDTH = 280f;
        public const float COMPACT_CARD_WIDTH = 260f;

        public static int Columns(bool compact) =>
            compact ? COMPACT_COLUMNS : COLUMNS;

        public static float CardWidth(bool compact) =>
            compact ? COMPACT_CARD_WIDTH : CARD_WIDTH;

        public static void Apply(GridLayoutGroup grid, bool compact)
        {
            grid.constraintCount = Columns(compact);
            grid.cellSize = new Vector2(CardWidth(compact), grid.cellSize.y);
        }

        /// <summary>
        ///     Re-flows the hand-placed loading cards, authored <see cref="COLUMNS" /> per row: each keeps its row, the columns close
        ///     up by the width change, and the columns beyond the row are hidden.
        /// </summary>
        public static void LayoutLoadingCards(RectTransform[] cards, bool compact)
        {
            int columns = Columns(compact);
            float width = CardWidth(compact);

            for (var i = 0; i < cards.Length; i++)
            {
                RectTransform card = cards[i];
                int column = i % COLUMNS;
                Vector2 size = card.sizeDelta;
                float widthChange = width - size.x;

                card.gameObject.SetActive(column < columns);
                card.anchoredPosition += new Vector2((column + card.pivot.x) * widthChange, 0f);
                card.sizeDelta = new Vector2(width, size.y);
            }
        }
    }
}
