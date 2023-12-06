using DCL.MapRenderer.ConsumerUtils;
using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Minimap
{
    public class MinimapView : ViewBase, IView {

        [field: SerializeField]
        internal RawImage mapRendererTargetImage { get; private set; }

        [field: SerializeField]
        internal Button minimapRendererButton { get; private set; }

        [field: SerializeField]
        internal Button sideMenuButton { get; private set; }

        [field: SerializeField]
        internal GameObject sideMenu { get; private set; }

        [field: SerializeField]
        internal PixelPerfectMapRendererTextureProvider pixelPerfectMapRendererTextureProvider { get; private set; }

        [field: SerializeField]
        internal int mapRendererVisibleParcels { get; private set; }

        [field: SerializeField]
        internal Button expandMinimapButton { get; private set; }

        [field: SerializeField]
        internal GameObject arrowDown { get; private set; }

        [field: SerializeField]
        internal GameObject arrowUp { get; private set; }

        [field: SerializeField]
        internal TMP_Text placeNameText { get; private set; }

        [field: SerializeField]
        internal TMP_Text placeCoordinatesText  { get; private set; }

        [field: SerializeField]
        internal RectTransform minimapContainer  { get; private set; }
    }
}
