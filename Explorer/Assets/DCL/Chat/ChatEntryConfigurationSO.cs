using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = System.Random;

namespace DCL.Chat
{
    [CreateAssetMenu(fileName = "ChatEntryConfiguration", menuName = "SO/ChatEntryConfiguration")]
    public class ChatEntryConfigurationSO : ScriptableObject
    {
        [SerializeField] public List<Color> nameColors;

        private int seed;
        private byte[] asciiValues;

        public Color GetNameColor(string username)
        {
            seed = 0;
            asciiValues = Encoding.ASCII.GetBytes(username);
            foreach (byte value in asciiValues)
                seed += value;

            Random rand1 = new Random(seed);
            return nameColors[rand1.Next(nameColors.Count)];
        }
    }
}
