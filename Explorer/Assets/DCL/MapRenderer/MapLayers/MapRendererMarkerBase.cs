using UnityEngine;

namespace DCL.MapRenderer.MapLayers
{
    public class MapRendererMarkerBase : MonoBehaviour
    {
        [field: SerializeField]
        internal Vector2 pivot { get; private set; } = new (0.5f, 0.5f);
    }
}
