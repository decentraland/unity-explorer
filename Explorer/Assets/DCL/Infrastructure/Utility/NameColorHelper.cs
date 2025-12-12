using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = System.Random;

namespace DCL.Utilities
{
    public static class NameColorHelper
    {
        private static readonly Color DEFAULT_COLOR = Color.white;
        private static IReadOnlyList<Color> nameColors = Array.Empty<Color>();

        public static void SetNameColors(IReadOnlyList<Color> colors)
        {
            nameColors = colors;
        }

        public static Color GetNameColor(string? username)
        {
            if (nameColors.Count == 0 || string.IsNullOrEmpty(username)) return DEFAULT_COLOR;

            uint seed = 0;

            int size = Encoding.ASCII.GetByteCount(username);
            Span<byte> bytes = stackalloc byte[size];

            Encoding.ASCII.GetBytes(username, bytes);

            foreach (byte value in bytes)
                seed += value;

            var rand1 = new Unity.Mathematics.Random(seed);
            return nameColors[rand1.NextInt(nameColors.Count)];
        }
    }
}
