using System.Text;
using UnityEngine;
using Random = System.Random;

namespace DCL.Chat
{
    [CreateAssetMenu(fileName = "ChatEntryConfiguration", menuName = "SO/ChatEntryConfiguration")]
    public class ChatEntryConfigurationSO : ScriptableObject
    {
        [SerializeField] public Sprite otherUsersBackground;
        [SerializeField] public Sprite otherUsersVerifiedIcon;
        [SerializeField] public Color otherUsersEntryColor;

        [SerializeField] public Sprite ownUsersBackground;
        [SerializeField] public Sprite ownUserVerifiedIcon;
        [SerializeField] public Color ownUsersEntryColor;

        [SerializeField] public float nameColorSaturation;
        [SerializeField] public float nameColorValue;

        private int seed;
        private byte[] asciiValues;

        public Color GetNameColor(string username)
        {
            seed = 0;
            asciiValues = Encoding.ASCII.GetBytes(username);
            foreach (byte value in asciiValues)
                seed += value;

            Random rand1 = new Random(seed);
            return Color.HSVToRGB((float) rand1.NextDouble(), nameColorSaturation, nameColorValue);
        }
    }
}
