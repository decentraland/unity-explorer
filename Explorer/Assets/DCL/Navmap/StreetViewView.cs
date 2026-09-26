using DCL.MapRenderer.ConsumerUtils;
using UnityEngine;

namespace DCL.Navmap
{
    public class StreetViewView : MonoBehaviour
    {
        [field: SerializeField]
        public MapRenderImage StreetViewRenderImage { get; private set; }

        [field: SerializeField]
        public PixelPerfectMapRendererTextureProvider StreetViewPixelPerfectMapRendererTextureProvider { get; private set; }
    }
}
