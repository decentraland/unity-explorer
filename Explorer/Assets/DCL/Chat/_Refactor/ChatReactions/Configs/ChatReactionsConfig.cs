using UnityEngine;
using Utility;

namespace DCL.Chat.ChatReactions.Configs
{
    /// <summary>
    /// Root configuration asset for the entire chat reactions feature.
    /// Assign this to ChatPluginSettings.ReactionsConfig in the PluginSettingsContainer.
    /// Groups shared rendering assets, per-lane tuning, message reactions, and debug settings.
    /// </summary>
    [CreateAssetMenu(fileName = "ChatReactionsConfig",
                     menuName = "DCL/Chat/Reactions/Chat Reactions Config")]
    public class ChatReactionsConfig : ScriptableObject
    {
        [field: Header("Shared")]
        [field: Note("INIT-ONLY — atlas descriptor. Texture and tile layout are applied to the material at init, " +
                     "overwriting any values baked into the material asset.")]
        [field: SerializeField] public ChatReactionsAtlasConfig Atlas { get; private set; }

        [field: Note("INIT-ONLY — GPU-instanced unlit material cloned at init. " +
                     "The source material is never mutated at runtime.")]
        [field: SerializeField] public Material EmojiMaterial { get; private set; }

        [field: Header("Lanes")]
        [field: Note("Per-lane tuning ScriptableObjects. Click to expand each sub-config.")]
        [field: SerializeField] public ChatReactionsUILaneConfig UILane { get; private set; }
        [field: SerializeField] public ChatReactionsWorldLaneConfig WorldLane { get; private set; }

        [field: Header("Shared Spawn Settings")]
        [field: SerializeField] [field: Range(0.05f, 1f)]
        public float SpawnSizeMinRatio { get; private set; } = 0.2f;

        [field: SerializeField] [field: Range(0.1f, 1f)]
        public float SpawnSizeMaxRatio { get; private set; } = 0.5f;

        /// <summary>Safe tile count: at least 1, even if atlas is missing.</summary>
        public int SafeTotalTiles => Atlas != null ? Mathf.Max(1, Atlas.TotalTiles) : 1;

        [field: Header("Message Reactions")]
        [field: SerializeField] public ChatReactionsMessageConfig MessageReactions { get; private set; }

        [Header("Debug — Networking")]
        [Note("Use mock reaction simulation instead of LiveKit. " +
              "Nearby avatars will appear to send random reactions without a real connection.")]
        public bool MockEnabled;

        [Note("Echo your own reactions back to yourself for testing without a second client.")]
        public bool SelfSendEnabled;

        [Header("Debug — Streaming")]
        [Note("Master toggle — enables debug toggles and live stats. Disable in production.")]
        public bool DebugEnabled;

        [Note("Continuously stream UI particles from the lane bottom.")]
        public bool StreamUILane;

        [Note("Continuously stream world particles above the local player's head.")]
        public bool StreamLocalPlayer;

        [Note("Continuously spawn random reactions above all nearby remote avatars.")]
        public bool StreamRemotePlayers;
    }
}
