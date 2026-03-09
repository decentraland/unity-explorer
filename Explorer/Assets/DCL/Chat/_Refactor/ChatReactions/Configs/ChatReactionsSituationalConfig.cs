using UnityEngine;

namespace DCL.Chat.ChatReactions.Configs
{
    /// <summary>
    /// Settings for the GPU-instanced emoji burst system (situational reactions).
    /// Groups the atlas, material, and per-lane tuning assets into one place.
    /// </summary>
    [CreateAssetMenu(fileName = "ChatReactionsSituationalConfig",
                     menuName = "DCL/Chat/Reactions/Situational Config")]
    public class ChatReactionsSituationalConfig : ScriptableObject
    {
        [field: Tooltip("Atlas descriptor. The controller reads this at init and applies the texture " +
                        "and tile layout to the material — overwriting any values baked into the material asset.")]
        [field: SerializeField] public ChatReactionsAtlasConfig Atlas { get; private set; }

        [field: Tooltip("GPU-instanced unlit material. Per-frame properties (GlobalAlpha) are always " +
                        "pushed via MaterialPropertyBlock — the material itself is never mutated at runtime.")]
        [field: SerializeField] public Material EmojiMaterial { get; private set; }

        [field: Header("Lanes")]
        [field: SerializeField] public ChatReactionsUILaneConfig UILane { get; private set; }
        [field: SerializeField] public ChatReactionsWorldLaneConfig WorldLane { get; private set; }

        [field: Header("Debug")]
        [field: Tooltip("Optional. Assign to enable Inspector toggles and live stats for testing. Leave null in production.")]
        [field: SerializeField] public ChatReactionsDebugConfig DebugConfig { get; private set; }
    }
}
