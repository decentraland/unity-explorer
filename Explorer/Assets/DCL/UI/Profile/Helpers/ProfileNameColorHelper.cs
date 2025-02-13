using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace DCL.UI.Profiles.Helpers
{
    public class ProfileNameColorHelper : IProfileNameColorHelper
    {
        private static readonly Color DEFAULT_COLOR = Color.white;

        private readonly List<Color> nameColors;

        public ProfileNameColorHelper(List<Color> nameColors)
        {
            this.nameColors = nameColors;
        }

        public Color GetNameColor(string username)
        {
            if (nameColors.Count == 0) return DEFAULT_COLOR;

            int seed = username.GetHashCode();
            var random = new Random(seed);
            return nameColors[random.Next(nameColors.Count)];
        }
    }
}
