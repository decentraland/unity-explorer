using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.Input
{
    [CreateAssetMenu(fileName = "CursorSettings", menuName = "DCL/Various/Cursor Settings")]
    public class CursorSettings : ScriptableObject
    {
        [field: SerializeField] public AssetReferenceTexture2D NormalCursor { get; private set; }
        [field: SerializeField] public Vector2 NormalCursorHotspot { get; private set; }

        [field: SerializeField] public AssetReferenceTexture2D InteractionCursor { get; private set; }
        [field: SerializeField] public Vector2 InteractionCursorHotspot { get; private set; }
    }
}
