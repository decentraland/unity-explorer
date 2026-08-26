using UnityEngine;

namespace OutfitStudio
{
    /// <summary>
    /// A named outfit saved as a project asset. Created from the Outfit Studio window
    /// (or via the asset create menu) so artists can keep a local library of looks.
    /// </summary>
    [CreateAssetMenu(menuName = "Decentraland/Outfit Preset", fileName = "OutfitPreset")]
    public class OutfitPreset : ScriptableObject
    {
        public OutfitDefinition outfit = new();
    }
}
