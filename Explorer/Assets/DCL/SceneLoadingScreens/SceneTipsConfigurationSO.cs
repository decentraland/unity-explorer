using UnityEngine;

namespace DCL.SceneLoadingScreens
{
    [CreateAssetMenu(fileName = "Scene Tips Configuration", menuName = "DCL/Various/SceneTipsConfiguration")]
    public class SceneTipsConfigurationSO : ScriptableObject
    {
        [SerializeField] private Color[] backgroundColors = null!;

        public Color GetColor(int index) =>
            backgroundColors[index % backgroundColors.Length];
    }
}
