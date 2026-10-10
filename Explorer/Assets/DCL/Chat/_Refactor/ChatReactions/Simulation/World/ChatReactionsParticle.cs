using UnityEngine;

namespace DCL.Chat.ChatReactions.Simulation.World
{
    public struct ChatReactionsParticle : IAliveParticle
    {
        public const byte ANCHOR_NONE = 255;

        public Vector3 pos;
        public Vector3 vel;
        public float age;
        public float lifetime;
        public float startSize;
        public float endSize;
        public int emojiIndex;
        public float zigZagPhase;
        public byte alive;
        public byte anchorIndex;

        public byte Alive => alive;
    }
}
