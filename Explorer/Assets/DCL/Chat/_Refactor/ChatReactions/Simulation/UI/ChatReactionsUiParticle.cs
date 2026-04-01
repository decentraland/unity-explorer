using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    public struct ChatReactionsUiParticle : IAliveParticle
    {
        public Vector2 screenPos;
        public Vector2 screenVel;
        public float age;
        public float lifetime;
        public float startSizePx;
        public float endSizePx;
        public int emojiIndex;
        public float zigZagPhase;
        public byte alive;

        public byte Alive => alive;
    }
}
