using DCL.MapRenderer.ConsumerUtils;
using DCL.UI.Buttons;
using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Minimap
{
    public class MinimapView : ViewBase, IView
    {
        private static readonly int HOVER = Animator.StringToHash("Hover");
        private static readonly int UNHOVER = Animator.StringToHash("Unhover");

        [field: SerializeField]
        internal RawImage mapRendererTargetImage { get; private set; }

        [field: SerializeField]
        internal HoverableButton minimapRendererButton { get; private set; }

        [field: SerializeField]
        internal Button sideMenuButton { get; private set; }

        [field: SerializeField]
        internal CanvasGroup SideMenuCanvasGroup { get; private set; }

        [field: SerializeField]
        internal PixelPerfectMapRendererTextureProvider pixelPerfectMapRendererTextureProvider { get; private set; }

        [field: SerializeField]
        internal int mapRendererVisibleParcels { get; private set; }

        [field: SerializeField]
        internal Button expandMinimapButton { get; private set; }

        [field: SerializeField]
        internal Button collapseMinimapButton { get; private set; }

        [field: SerializeField]
        internal TMP_Text placeNameText { get; private set; }

        [field: SerializeField]
        internal TMP_Text placeCoordinatesText  { get; private set; }

        [field: SerializeField]
        internal RectTransform minimapContainer  { get; private set; }

        [field: SerializeField]
        internal SideMenuView sideMenuView  { get; private set; }

        [field: SerializeField]
        internal Animator minimapAnimator  { get; private set; }

        private void Start()
        {
            minimapRendererButton.OnButtonHover += OnHoverMap;
            minimapRendererButton.OnButtonUnhover += OnUnHoverMap;
        }

        private void OnHoverMap() =>
            minimapAnimator.SetTrigger(HOVER);

        private void OnUnHoverMap() =>
            minimapAnimator.SetTrigger(UNHOVER);

    }
}
