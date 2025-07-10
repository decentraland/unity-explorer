using DG.Tweening;
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
        
        [field: Header("Animations")]
        [field: Tooltip("The time in seconds it takes for the main panels to fade in/out.")]
        [field: SerializeField]
        public float PanelsFadeDuration { get; private set; } = 0.2f;

        [field: Tooltip("The easing function to use for the panel fade animation.")]
        [field: SerializeField]
        public Ease PanelsFadeEase { get; private set; } = Ease.OutQuad;
    }
}
