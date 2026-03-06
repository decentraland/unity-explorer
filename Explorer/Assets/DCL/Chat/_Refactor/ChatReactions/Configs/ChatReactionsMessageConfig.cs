using UnityEngine;

namespace DCL.Chat.ChatReactions.Configs
{
    /// <summary>
    /// Settings for per-message emoji reactions shown inline in the chat feed
    /// (similar to Discord / Slack message reactions).
    /// </summary>
    [CreateAssetMenu(fileName = "ChatReactionsMessageConfig",
                     menuName = "DCL/Chat/Reactions/Message Config")]
    public class ChatReactionsMessageConfig : ScriptableObject
    {
        [field: Header("Picker")]
        [field: Tooltip("Atlas tile indices of the emojis available in the reaction picker. " +
                        "Must be the same length as ReactionPickerIcons.")]
        [field: SerializeField] public int[] AvailableEmojiIndices { get; private set; } = System.Array.Empty<int>();

        [field: Tooltip("Sprite icons shown in the reaction picker UI, one per available emoji. " +
                        "Must be the same length as AvailableEmojiIndices.")]
        [field: SerializeField] public Sprite[] ReactionPickerIcons { get; private set; } = System.Array.Empty<Sprite>();

        [field: Header("Selector Defaults")]
        [field: Tooltip("Atlas tile indices shown in the reaction selector on first launch " +
                        "(before the user customizes). Keep this short (3–5 items).")]
        [field: SerializeField] public int[] DefaultFavoriteEmojiIndices { get; private set; } = System.Array.Empty<int>();

        [field: Header("Behaviour")]
        [field: Min(1)]
        [field: Tooltip("Maximum number of distinct reaction types displayed on a single message.")]
        [field: SerializeField] public int MaxReactionTypesPerMessage { get; private set; } = 6;

        [field: Min(0f)]
        [field: Tooltip("Seconds to wait after the last reaction toggle before sending an update to the network. " +
                        "Prevents flooding the backend with rapid taps.")]
        [field: SerializeField] public float NetworkDebounceSeconds { get; private set; } = 0.5f;

        [field: Header("Animations")]
        [field: Min(0f)]
        [field: Tooltip("Duration of the reaction bubble appear animation (seconds).")]
        [field: SerializeField] public float AppearDuration { get; private set; } = 0.15f;

        [field: Min(0f)]
        [field: Tooltip("Duration of the reaction bubble disappear animation (seconds).")]
        [field: SerializeField] public float DisappearDuration { get; private set; } = 0.1f;

        [field: Min(0f)]
        [field: Tooltip("Duration of the bounce/pop animation played when a reaction count increments (seconds).")]
        [field: SerializeField] public float CountIncrementBounceDuration { get; private set; } = 0.12f;
    }
}
