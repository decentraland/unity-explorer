using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = System.Random;

namespace DCL.UI.Profiles.Helpers
{
    public class ProfileNameColorHelper : IProfileNameColorHelper
    {
        private static readonly Color DEFAULT_COLOR = Color.white;

        private readonly List<Color> nameColors;
        private byte[] asciiValues;
        private int seed;

        public ProfileNameColorHelper(List<Color> nameColors)
        {
            this.nameColors = nameColors;
        }

        public Color GetNameColor(string username)
        {
            if (nameColors.Count == 0) return DEFAULT_COLOR;

            seed = 0;
            asciiValues = Encoding.ASCII.GetBytes(username);

            foreach (byte value in asciiValues)
                seed += value;

            var rand1 = new Random(seed);
            return nameColors[rand1.Next(nameColors.Count)];
        }
    }
}
