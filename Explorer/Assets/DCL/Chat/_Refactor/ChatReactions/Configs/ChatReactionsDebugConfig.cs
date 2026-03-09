using UnityEngine;

namespace DCL.Chat.ChatReactions.Configs
{
    /// <summary>
    /// Debug/test toggles and live stats for the chat reaction particle system.
    /// Assign to <see cref="ChatReactionsSituationalConfig"/> (optional) to enable
    /// Inspector-driven testing. Stats are updated each frame by the presenter.
    /// </summary>
    [CreateAssetMenu(fileName = "ChatReactionsDebugConfig",
                     menuName = "DCL/Chat/Reactions/Debug Config")]
    public class ChatReactionsDebugConfig : ScriptableObject
    {
        [Header("UI Lane")]
        [Tooltip("Continuously stream UI particles from the lane bottom.")]
        public bool StreamUILane;

        [Header("World Lane - Local Player")]
        [Tooltip("Continuously stream world particles above the local player's head.")]
        public bool StreamLocalPlayer;

        [Header("World Lane - Remote Players")]
        [Tooltip("Continuously spawn random reactions above all nearby remote avatars.")]
        public bool StreamRemotePlayers;

        [Header("Stats (read-only at runtime)")]
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
