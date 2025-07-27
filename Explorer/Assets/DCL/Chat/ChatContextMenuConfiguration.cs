using UnityEngine;

namespace DCL.Chat
{
    [CreateAssetMenu(fileName = "ChatContextMenuSettings", menuName = "DCL/Chat/ChatMenuSettings")]
    public class ChatContextMenuConfiguration : ScriptableObject
    {
        [field: SerializeField] public int ContextMenuWidth { get; private set; } = 218;
        [field: SerializeField] public int ElementsSpacing { get; private set; } = 5;
        [field: SerializeField] public Vector2 OffsetFromTarget { get; private set; }
        [field: SerializeField] public RectOffset VerticalPadding { get; private set; } = null!;

        [field: SerializeField] public Sprite DeleteChatHistorySprite { get; private set; } = null!;
        [field: SerializeField] public string DeleteChatHistoryText { get; private set; } = "Delete Chat History";

        [field: SerializeField] public Sprite NotificationPingSprite { get; private set; } = null!;
        [field: SerializeField] public string NotificationPingText { get; private set; } = "Notification Ping";
    }
}
