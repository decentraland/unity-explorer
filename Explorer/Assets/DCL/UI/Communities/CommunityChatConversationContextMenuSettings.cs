using System;
using UnityEngine;

namespace DCL.UI.Communities
{
    [CreateAssetMenu(fileName = "CommunityChatConversationContextMenuSettings", menuName = "DCL/Communities/CommunityChatConversationContextMenuSettings")]
    [Serializable]
    public class CommunityChatConversationContextMenuSettings : ScriptableObject
    {
        [SerializeField] private Sprite viewCommunitySprite;
        [SerializeField] private string viewCommunityText = "View Community";

        public Sprite ViewCommunitySprite => viewCommunitySprite;
        public string ViewCommunityText => viewCommunityText;
    }
}
