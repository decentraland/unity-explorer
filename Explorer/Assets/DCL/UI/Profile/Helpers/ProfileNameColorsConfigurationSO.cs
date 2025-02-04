using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace DCL.UI.Profiles.Helpers
{
    [CreateAssetMenu(fileName = "ProfileNameColorsConfiguration", menuName = "DCL/UI/Profile Name Colors Configuration")]
    public class ProfileNameColorsConfigurationSO : ScriptableObject, IProfileNameColorHelper
    {
        [SerializeField] private List<Color> nameColors;

        public Color GetNameColor(string username)
        {
            int seed = username.GetHashCode();
            var random = new Random(seed);
            return nameColors[random.Next(nameColors.Count)];
        }
    }
}
