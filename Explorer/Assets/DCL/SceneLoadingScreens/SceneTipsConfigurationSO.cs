using UnityEngine;

namespace DCL.SceneLoadingScreens
{
    [CreateAssetMenu(menuName = "DCL/Tips/Configuration", fileName = "SceneTipsConfiguration")]
    public class SceneTipsConfigurationSO : ScriptableObject
    {
        [SerializeField] private Color[] backgroundColors = null!;

        public Color GetColor(int index) =>
            backgroundColors[index];
    }
}
