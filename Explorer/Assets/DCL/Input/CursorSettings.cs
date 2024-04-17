using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.Input
{
    [CreateAssetMenu(menuName = "Create CursorSettings", fileName = "CursorSettings", order = 0)]
    public class CursorSettings : ScriptableObject
    {
        [field: SerializeField] public AssetReferenceTexture2D NormalCursor { get; private set; }
        [field: SerializeField] public AssetReferenceTexture2D InteractionCursor { get; private set; }
    }
}
