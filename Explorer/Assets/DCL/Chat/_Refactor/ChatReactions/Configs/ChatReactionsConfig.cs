using UnityEngine;

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
        [field: Tooltip("Atlas descriptor. The controller reads this at init and applies the texture " +
                        "and tile layout to the material — overwriting any values baked into the material asset.")]
        [field: SerializeField] public ChatReactionsAtlasConfig Atlas { get; private set; }

        [field: Tooltip("GPU-instanced unlit material. Per-frame properties (GlobalAlpha) are always " +
                        "pushed via MaterialPropertyBlock — the material itself is never mutated at runtime.")]
        [field: SerializeField] public Material EmojiMaterial { get; private set; }

        [field: Header("Lanes")]
        [field: SerializeField] public ChatReactionsUILaneConfig UILane { get; private set; }
        [field: SerializeField] public ChatReactionsWorldLaneConfig WorldLane { get; private set; }

        [field: Header("Message Reactions")]
        [field: SerializeField] public ChatReactionsMessageConfig MessageReactions { get; private set; }

        [Header("Debug")]
        [Tooltip("Enable Inspector-driven debug toggles and live stats. Disable in production.")]
        public bool DebugEnabled;

        [Tooltip("Continuously stream UI particles from the lane bottom.")]
        public bool StreamUILane;

        [Tooltip("Continuously stream world particles above the local player's head.")]
        public bool StreamLocalPlayer;

        [Tooltip("Continuously spawn random reactions above all nearby remote avatars.")]
        public bool StreamRemotePlayers;

        [Header("Debug Stats (read-only at runtime)")]
        [SerializeField] private int uiAliveCount;
        [SerializeField] private int uiPoolCapacity;
        [SerializeField] private int worldAliveCount;
        [SerializeField] private int worldPoolCapacity;
        [SerializeField] private int nearbyAvatarCount;
        [SerializeField] private bool isUIStreaming;
        [SerializeField] private bool isWorldStreaming;
        [SerializeField] private bool isDebugNearbyActive;

        /// <summary>Called by the presenter each frame to push live data into the config for Inspector display.</summary>
        public void UpdateStats(int uiAlive, int uiCapacity,
            int worldAlive, int worldCapacity,
            int nearbyAvatars,
            bool uiStreaming, bool worldStreaming,
            bool debugNearby)
        {
            uiAliveCount = uiAlive;
            uiPoolCapacity = uiCapacity;
            worldAliveCount = worldAlive;
            worldPoolCapacity = worldCapacity;
            nearbyAvatarCount = nearbyAvatars;
            isUIStreaming = uiStreaming;
            isWorldStreaming = worldStreaming;
            isDebugNearbyActive = debugNearby;
        }
    }
}
