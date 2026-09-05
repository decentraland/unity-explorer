using System.Collections.Generic;
using UnityEngine;

namespace DCL.UI
{
    [CreateAssetMenu(fileName = "ColorPresets", menuName = "DCL/Various/Color Presets")]
    public class ColorPresetsSO : ScriptableObject
    {
        public List<Color> colors;
    }
}
