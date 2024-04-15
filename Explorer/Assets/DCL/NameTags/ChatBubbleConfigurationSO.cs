using System.ComponentModel;
using UnityEngine;

namespace DCL.Nametags
{
    [CreateAssetMenu(fileName = "ChatBubbleConfiguration", menuName = "SO/ChatBubbleConfiguration")]
    public class ChatBubbleConfigurationSO : ScriptableObject
    {
        public float nametagMarginOffsetWidth;
        public float nametagMarginOffsetHeight;
        public float bubbleMarginOffsetWidth;
        public float bubbleMarginOffsetHeight;
        public float animationDuration;
        public float fullOpacityMaxDistance;
        public int bubbleIdleTime;
        public float maxDistance;

        [Description("Additional ms wait per character when displaying a chat message in a bubble")]
        public int additionalMsPerCharacter;
    }
}
