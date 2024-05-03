using System.ComponentModel;
using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.Nametags
{
    [CreateAssetMenu(fileName = "ChatBubbleConfiguration", menuName = "SO/ChatBubbleConfiguration")]
    public class ChatBubbleConfigurationSO : ScriptableObject
    {
        public float nametagMarginOffsetWidth;
        public float nametagMarginOffsetHeight;
        public float bubbleMarginOffsetWidth;
        public float bubbleMarginOffsetHeight;
        public float animationInDuration;
        public float animationOutDuration;
        public float fullOpacityMaxDistance;
        public int bubbleIdleTime;
        public float maxDistance;
        public float singleEmojiExtraHeight;
        public float singleEmojiSize;

        [Description("Additional ms wait per character when displaying a chat message in a bubble")]
        public int additionalMsPerCharacter;
    }
}
