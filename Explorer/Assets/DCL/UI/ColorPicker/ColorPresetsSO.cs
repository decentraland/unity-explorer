using System.Collections.Generic;
using UnityEngine;

namespace DCL.UI
{
    [CreateAssetMenu(fileName = "ColorPresets", menuName = "SO/ColorPresets")]
    public class ColorPresetsSO : ScriptableObject
    {
        public List<Color> colors;
    }
}
