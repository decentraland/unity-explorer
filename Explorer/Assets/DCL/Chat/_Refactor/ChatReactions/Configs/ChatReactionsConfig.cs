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
        [field: Header("SHARED")]
        [field: Note("INIT-ONLY — atlas descriptor. Texture and tile layout are applied to the material at init, " +
                     "overwriting any values baked into the material asset.")]
        [field: SerializeField] public ChatReactionsAtlasConfig Atlas { get; private set; }

        [field: Note("INIT-ONLY — GPU-instanced unlit material cloned at init. " +
                     "The source material is never mutated at runtime.")]
        [field: SerializeField] public Material EmojiMaterial { get; internal set; }

        [field: Header("LANES")]
        [field: Note("Per-lane tuning ScriptableObjects. Click to expand each sub-config.")]
        [field: SerializeField] public ChatReactionsUILaneConfig UILane { get; private set; }
        [field: SerializeField] public ChatReactionsWorldLaneConfig WorldLane { get; internal set; }

        [field: Header("SHARED SPAWN SETTINGS")]
        [field: Note("Minimum spawn scale ratio. Particles start at this fraction of their final size " +
                     "and grow to full. 0.2 = particles pop in at 20% size.")]
        [field: SerializeField] [field: Range(0.05f, 1f)]
        public float SpawnSizeMinRatio { get; private set; } = 0.2f;

        [field: Note("Maximum spawn scale ratio. Particles start at a random ratio between min and max. " +
                     "0.5 = up to 50% of final size at birth.")]
        [field: SerializeField] [field: Range(0.1f, 1f)]
        public float SpawnSizeMaxRatio { get; private set; } = 0.5f;

        /// <summary>Safe tile count: at least 1, even if atlas is missing.</summary>
        public int SafeTotalTiles => Atlas != null ? Mathf.Max(1, Atlas.TotalTiles) : 1;

        [field: Header("MESSAGE REACTIONS")]
        [field: SerializeField] public ChatReactionsMessageConfig MessageReactions { get; private set; }

        [Header("DEBUG — STREAMING")]
        [Note("Master toggle — enables debug toggles and live stats. Disable in production.")]
        public bool DebugEnabled;

        [Note("Continuously stream UI particles from the lane bottom.")]
        public bool StreamUILane;

        [Note("Continuously stream world particles above the local player's head.")]
        public bool StreamLocalPlayer;

        [Note("Continuously spawn random reactions above all nearby remote avatars.")]
        public bool StreamRemotePlayers;

        [Header("DEBUG — STREAM COMMAND")]
        [Note("Default reactions/s emitted locally by /streamreactions.")]
        [Range(1f, 60f)]
        public float StreamCommandEmitRate = 15f;

        [Note("Default max reactions/s sent to network during streaming.")]
        [Range(1f, 60f)]
        public float StreamCommandSendBudget = 10f;

        [Header("DEBUG — RECEIVE LIMITING")]
        [Note("Max queued remote reactions. Newest dropped when exceeded. 0 = unlimited.")]
        [Range(0, 500)]
        public int MaxReceiveQueueDepth = 120;

        [Note("Queue depth where dynamic stagger ramp begins. Must be less than MaxReceiveQueueDepth for the ramp to take effect.")]
        [Range(0, 100)]
        public int DynamicStaggerRampStart = 15;

        [Note("Stagger floor at max queue depth. 0 = drain all instantly.")]
        [Range(0f, 0.5f)]
        public float MinStaggerInterval;

        [Note("Max remote reactions/s shown in UI lane. World particles use per-avatar cap. 0 = unlimited.")]
        [Range(0f, 200f)]
        public float MaxRemoteUIReactionsPerSec = 120f;

        [Header("DYNAMIC SCALING")]
        [Note("Master toggle for pool-pressure-based per-avatar cap scaling. " +
              "When disabled, static MaxParticlesPerAvatar is used unchanged.")]
        public bool DynamicScalingEnabled = true;

        [Note("Fraction of world pool capacity to distribute among active avatars. " +
              "0.7 = 70% of 1023 = 716 particles shared equally. " +
              "Remaining 30% acts as headroom for bursts and timing overlap.")]
        [Range(0.3f, 0.95f)]
        public float WorldPoolTargetUtilization = 0.7f;
    }
}
