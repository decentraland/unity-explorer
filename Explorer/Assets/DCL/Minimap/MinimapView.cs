using DCLServices.MapRenderer.ConsumerUtils;
using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Minimap
{
    public class MinimapView : ViewBase, IView {

        [field: SerializeField]
        internal RawImage mapRendererTargetImage { get; private set; }

        [field: SerializeField]
        internal PixelPerfectMapRendererTextureProvider pixelPerfectMapRendererTextureProvider { get; private set; }

        [field: SerializeField]
        internal int mapRendererVisibleParcels { get; private set; }
    }
}
