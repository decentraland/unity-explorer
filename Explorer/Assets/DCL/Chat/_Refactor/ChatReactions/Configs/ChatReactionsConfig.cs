using UnityEngine;

namespace DCL.Chat.ChatReactions.Configs
{
    /// <summary>
    /// Root configuration asset for the entire chat reactions feature.
    /// Assign this to ChatPluginSettings.ReactionsConfig in the PluginSettingsContainer.
    /// </summary>
    [CreateAssetMenu(fileName = "ChatReactionsConfig",
                     menuName = "DCL/Chat/Reactions/Chat Reactions Config")]
    public class ChatReactionsConfig : ScriptableObject
    {
        [field: SerializeField] public ChatReactionsSituationalConfig SituationalReactions { get; private set; }
        [field: SerializeField] public ChatReactionsMessageConfig MessageReactions { get; private set; }
    }
}
