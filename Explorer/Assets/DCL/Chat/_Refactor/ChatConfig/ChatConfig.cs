using UnityEngine;

namespace DCL.Chat
{
    [CreateAssetMenu(fileName = "ChatConfig", menuName = "DCL/Chat/ChatConfig")]
    public class ChatConfig : ScriptableObject
    {
        [SerializeField] private string DCL_SYSTEM_SENDER = "DCL System";
        
        [field: Header("Prefabs")]
        [field: SerializeField]
        public ChatConversationsToolbarViewItem ItemPrefab { get; private set; }

        [field: Header("Nearby Channel Specifics")]
        [field: SerializeField]
        public Sprite NearbyConversationIcon { get; private set; }
    
        [field: SerializeField]
        public string NearbyConversationName { get; private set; } = "Nearby";
    }
}
