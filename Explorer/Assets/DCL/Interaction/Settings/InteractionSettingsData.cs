using UnityEngine;

namespace DCL.Interaction.Settings
{
    public class InteractionSettingsData : ScriptableObject
    {
        [field: SerializeField] public Color ValidColor { get; private set; }
        [field: SerializeField] public Color InvalidColor { get; private set; }
        [field: SerializeField] public float Thickness { get; private set; }
    }
}
