using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = System.Random;

namespace DCL.Chat
{
    [CreateAssetMenu(fileName = "ChatEntryConfiguration", menuName = "DCL/Chat/Chat Entry Configuration")]
    public class ChatEntryConfigurationSO : ScriptableObject
    {
        [SerializeField] public List<Color> nameColors;
        private byte[] asciiValues;
        [field: SerializeField] public float BackgroundHeightOffset { private set; get; } = 56;
        [field: SerializeField] public float BackgroundWidthOffset { private set; get; }= 56;
        [field: SerializeField] public float MaxEntryWidth { private set; get; }= 246;
        [field: SerializeField] public float VerifiedBadgeWidth { private set; get; }= 15;

        private int seed;

        public Color GetNameColor(string username)
        {
            seed = 0;
            asciiValues = Encoding.ASCII.GetBytes(username);

            foreach (byte value in asciiValues)
                seed += value;

            var rand1 = new Random(seed);
            return nameColors[rand1.Next(nameColors.Count)];
        }
    }
}
