using UnityEngine;

namespace DCL.CharacterPreview
{
    /// <summary>
    ///     Contains serialized data only needed for the character preview
    ///     See CharacterPreviewController in the old renderer
    /// </summary>
    public class CharacterPreviewContainer : MonoBehaviour
    {
        [field: SerializeField]
        internal Transform parent { get; private set; }

        [field: SerializeField]
        internal new Camera camera { get; private set; }
    }
}
