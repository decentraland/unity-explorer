using DCL.Character.CharacterMotion.Components;
using DCL.MapRenderer.ConsumerUtils;
using DCL.UI.Buttons;
using MVC;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Minimap
{
    public class MinimapView : ViewBase, IView
    {
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
        internal RectTransform sdk6Label  { get; private set; }

        [field: SerializeField]
        internal RectTransform minimapContainer  { get; private set; }

        [field: SerializeField]
        internal SideMenuView sideMenuView  { get; private set; }

        [field: SerializeField]
        internal Animator minimapAnimator  { get; private set; }

        [field: SerializeField]
        internal Button goToGenesisCityButton { get; private set; }

        [field: SerializeField]
        internal RuntimeAnimatorController genesisCityAnimatorController { get; private set; }

        [field: SerializeField]
        internal RuntimeAnimatorController worldsAnimatorController { get; private set; }

        [field: SerializeField]
        internal List<GameObject> objectsToActivateForGenesis { get; private set; }

        [field: SerializeField]
        internal List<GameObject> objectsToActivateForWorlds { get; private set; }

        private void Start()
        {
            minimapRendererButton.OnButtonHover += OnHoverMap;
            minimapRendererButton.OnButtonUnhover += OnUnHoverMap;
        }

        private void OnHoverMap() =>
            minimapAnimator.SetTrigger(AnimationHashes.HOVER);

        private void OnUnHoverMap() =>
            minimapAnimator.SetTrigger(AnimationHashes.UNHOVER);

    }
}
