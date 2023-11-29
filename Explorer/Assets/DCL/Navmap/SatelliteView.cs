using DCL.MapRenderer.ConsumerUtils;
using UnityEngine;

namespace DCL.Navmap
{
    public class SatelliteView : MonoBehaviour
    {
        [field: SerializeField]
        public MapRenderImage SatelliteRenderImage { get; private set; }

        [field: SerializeField]
        public PixelPerfectMapRendererTextureProvider SatellitePixelPerfectMapRendererTextureProvider { get; private set; }
    }
}
