using UnityEngine;
using Utility;

namespace DCL.Chat.ChatReactions.Configs
{
    public readonly struct TooltipPositioningConfig
    {
        public readonly Vector2 Offset;
        public readonly float ArrowMinX;
        public readonly float ArrowMaxX;
        public readonly float ArrowXOffset;

        public TooltipPositioningConfig(Vector2 offset, float arrowMinX, float arrowMaxX, float arrowXOffset)
        {
            Offset = offset;
            ArrowMinX = arrowMinX;
            ArrowMaxX = arrowMaxX;
            ArrowXOffset = arrowXOffset;
        }
    }

    /// <summary>
    /// Settings for per-message emoji reactions shown inline in the chat feed
    /// (similar to Discord / Slack message reactions).
    /// </summary>
    [CreateAssetMenu(fileName = "ChatReactionsMessageConfig",
                     menuName = "DCL/Chat/Reactions/Message Config")]
    public class ChatReactionsMessageConfig : ScriptableObject
    {
        [field: Header("Picker")]
        [field: Note("NOT YET WIRED UP — atlas tile indices for the reaction picker. " +
                     "Must match ReactionPickerIcons length.")]
        [field: SerializeField] public int[] AvailableEmojiIndices { get; private set; } = System.Array.Empty<int>();

        [field: Note("NOT YET WIRED UP — sprite icons for the reaction picker, one per emoji.")]
        [field: SerializeField] public Sprite[] ReactionPickerIcons { get; private set; } = System.Array.Empty<Sprite>();

        [field: Header("Situational Shortcuts Bar")]
        [field: Note("Unicode codepoints of the fixed emojis in the shortcuts bar. " +
                     "Resolved to atlas tile indices at init via ChatReactionsAtlasConfig.")]
        [field: SerializeField] public uint[] FixedDefaultEmojiUnicodes { get; private set; } = System.Array.Empty<uint>();

        [field: Note("How many recently-used emoji slots appear after the divider in the shortcuts bar.")]
        [field: Range(1, 10)]
        [field: SerializeField] public int MaxRecentEmojis { get; private set; } = 3;

        [field: Header("Behaviour")]
        [field: Note("NOT YET WIRED UP — max distinct reaction types shown on a single message.")]
        [field: Range(1, 20)]
        [field: SerializeField] public int MaxReactionTypesPerMessage { get; private set; } = 6;

        [field: Note("NOT YET WIRED UP — debounce delay (seconds) before sending reaction updates to the network. " +
                     "Prevents flooding the backend with rapid taps.")]
        [field: Range(0f, 2f)]
        [field: SerializeField] public float NetworkDebounceSeconds { get; private set; } = 0.5f;

        [field: Header("Animations")]
        [field: Note("NOT YET WIRED UP — reaction bubble appear animation duration (seconds).")]
        [field: Range(0f, 1f)]
        [field: SerializeField] public float AppearDuration { get; private set; } = 0.15f;

        [field: Note("NOT YET WIRED UP — reaction bubble disappear animation duration (seconds).")]
        [field: Range(0f, 1f)]
        [field: SerializeField] public float DisappearDuration { get; private set; } = 0.1f;

        [field: Note("NOT YET WIRED UP — bounce/pop animation when a reaction count increments (seconds).")]
        [field: Range(0f, 1f)]
        [field: SerializeField] public float CountIncrementBounceDuration { get; private set; } = 0.12f;

        [field: Header("Shortcuts Bar Positioning")]
        [field: Note("Offset applied when positioning the message shortcuts bar near a reaction button.")]
        [field: SerializeField] public Vector2 ShortcutsBarOffset { get; private set; } = new (0f, 40f);

        [field: Header("Emoji Panel Positioning")]
        [field: Note("Offset applied to the + button position when opening the emoji panel from reactions.")]
        [field: SerializeField] public Vector2 EmojiPanelOffset { get; private set; } = new (0f, 0f);

        [field: Header("Tooltip Positioning")]
        [field: Note("Offset applied when positioning the tooltip above a reaction pill. " +
                     "X keeps the tooltip centered (typically 0), Y is the gap above the pill.")]
        [field: SerializeField] public Vector2 TooltipOffset { get; private set; } = new (0f, 12f);

        [field: Note("Minimum local X for the tooltip arrow (clamps to left edge of tooltip background).")]
        [field: SerializeField] public float TooltipArrowMinX { get; private set; } = -140f;

        [field: Note("Maximum local X for the tooltip arrow (clamps to right edge of tooltip background).")]
        [field: SerializeField] public float TooltipArrowMaxX { get; private set; } = 140f;

        [field: Note("Extra X offset added to the arrow position after centering on the pill. " +
                     "Use to fine-tune alignment if the arrow doesn't point at the pill center.")]
        [field: SerializeField] public float TooltipArrowXOffset { get; private set; } = 0f;

        public TooltipPositioningConfig TooltipConfig =>
            new (TooltipOffset, TooltipArrowMinX, TooltipArrowMaxX, TooltipArrowXOffset);
    }
}
