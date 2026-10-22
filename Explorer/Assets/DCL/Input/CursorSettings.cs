using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.Input
{
    [CreateAssetMenu(fileName = "CursorSettings", menuName = "DCL/Various/Cursor Settings")]
    public class CursorSettings : ScriptableObject
    {
        [field: SerializeField] public AssetReferenceTexture2D NormalCursor { get; private set; } = null!;
        [field: SerializeField] public Vector2 NormalCursorHotspot { get; private set; }

        [field: SerializeField] public AssetReferenceTexture2D InteractionCursor { get; private set; } = null!;
        [field: SerializeField] public Vector2 InteractionCursorHotspot { get; private set; }

        [field: SerializeField] public AssetReferenceTexture2D GrabCursor { get; private set; } = null!;
        [field: SerializeField] public Vector2 GrabCursorHotspot { get; private set; }

        [field: SerializeField] public AssetReferenceTexture2D DraggingCursor { get; private set; } = null!;
        [field: SerializeField] public Vector2 DraggingCursorHotspot { get; private set; }

        [field: SerializeField] public AssetReferenceTexture2D NotAllowedCursor { get; private set; } = null!;
        [field: SerializeField] public Vector2 NotAllowedCursorHotspot { get; private set; }
    }
}
