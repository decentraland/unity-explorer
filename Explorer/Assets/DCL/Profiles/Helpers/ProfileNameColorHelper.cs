using DCL.Diagnostics;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = System.Random;

namespace DCL.Profiles.Helpers
{
    public static class ProfileNameColorHelper
    {
        private static readonly Color DEFAULT_COLOR = Color.white;
        private static List<Color> nameColors = new ();
        private static byte[]? asciiValues;
        private static int seed;


        public static void SetNameColors(List<Color> colors)
        {
            nameColors = colors;
        }

        public static Color GetNameColor(string? username)
        {
            if (nameColors.Count == 0 || string.IsNullOrEmpty(username)) return DEFAULT_COLOR;

            if (username == null)
                return DEFAULT_COLOR;

            seed = 0;
            asciiValues = Encoding.ASCII.GetBytes(username);

            foreach (byte value in asciiValues)
                seed += value;

            var rand1 = new Random(seed);
            return nameColors[rand1.Next(nameColors.Count)];
        }
    }
}
