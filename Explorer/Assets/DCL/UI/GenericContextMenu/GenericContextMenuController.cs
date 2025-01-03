using Cysharp.Threading.Tasks;
using DCL.UI.GenericContextMenu.Controls;
using MVC;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.UI.GenericContextMenu
{
    public class GenericContextMenuController : ControllerBase<GenericContextMenuView, GenericContextMenuParameter>
    {
        private enum ContextMenuOpenDirection
        {
            BOTTOM_RIGHT,
            BOTTOM_LEFT,
            TOP_LEFT,
            TOP_RIGHT
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private readonly ControlsPoolManager controlsPoolManager;

        private RectTransform viewRectTransform;
        private bool isClosing;

        public GenericContextMenuController(ViewFactoryMethod viewFactory,
            ControlsPoolManager controlsPoolManager) : base(viewFactory)
        {
            this.controlsPoolManager = controlsPoolManager;
        }

        public override void Dispose()
        {
            base.Dispose();

            controlsPoolManager.Dispose();
        }

        protected override void OnViewInstantiated()
        {
            viewRectTransform = viewInstance!.GetComponent<RectTransform>();

            viewInstance!.BackgroundCloseButtonClicked += TriggerContextMenuClose;
        }

        protected override void OnBeforeViewShow()
        {
            isClosing = false;

            ConfigureContextMenu();
        }

        private void ConfigureContextMenu()
        {
            viewInstance!.ControlsContainer.sizeDelta = new Vector2(inputData.Config.Width, viewInstance!.ControlsContainer.sizeDelta.y);

            for (var i = 0; i < inputData.Config.ContextMenuSettings.Count; i++)
            {
                ContextMenuControlSettings config = inputData.Config.ContextMenuSettings[i];

                switch (config.ControlTypeType)
                {
                    case ContextMenuControlTypes.SEPARATOR:
                        controlsPoolManager.GetSeparator(config as SeparatorContextMenuControlSettings);
                        break;
                    case ContextMenuControlTypes.BUTTON_WITH_TEXT_AND_ICON:
                        GenericContextMenuButtonWithTextView button = controlsPoolManager.GetButton(config as ButtonContextMenuControlSettings);
                        button.ButtonComponent.onClick.AddListener(inputData.ControlsActions[i] as UnityEngine.Events.UnityAction);
                        button.ButtonComponent.onClick.AddListener(TriggerContextMenuClose);
                        break;
                    case ContextMenuControlTypes.TOGGLE_WITH_TEXT:
                        GenericContextMenuToggleView toggle = controlsPoolManager.GetToggle(config as ToggleContextMenuControlSettings);
                        toggle.ToggleComponent.onValueChanged.AddListener(inputData.ControlsActions[i] as UnityEngine.Events.UnityAction<bool>);
                        toggle.ToggleComponent.onValueChanged.AddListener(toggleValue => TriggerContextMenuClose());
                        break;
                }
            }

            viewInstance!.ControlsContainer.localPosition = GetControlsPosition(inputData.AnchorPosition, inputData.Config.OffsetFromTarget, inputData.OverlapRect);
        }

        private Vector2 GetOffsetByDirection(ContextMenuOpenDirection direction, Vector2 offsetFromTarget)
        {
            return direction switch
            {
                ContextMenuOpenDirection.BOTTOM_RIGHT => offsetFromTarget,
                ContextMenuOpenDirection.BOTTOM_LEFT => new Vector2(-offsetFromTarget.x - viewInstance!.ControlsContainer.rect.width, offsetFromTarget.y),
                ContextMenuOpenDirection.TOP_RIGHT => new Vector2(offsetFromTarget.x, -offsetFromTarget.y + viewInstance!.ControlsContainer.rect.height),
                ContextMenuOpenDirection.TOP_LEFT => new Vector2(-offsetFromTarget.x - viewInstance!.ControlsContainer.rect.width, -offsetFromTarget.y + viewInstance!.ControlsContainer.rect.height),
                _ => Vector3.zero
            };
        }

        private Vector3 GetControlsPosition(Vector2 anchorPosition, Vector2 offsetFromTarget, Rect? overlapRect)
        {
            Vector3 position = viewRectTransform.InverseTransformPoint(anchorPosition);
            position.x += viewInstance!.ControlsContainer.rect.width / 2;
            position.y -= viewInstance!.ControlsContainer.rect.height / 2;

            Vector3 newPosition = Vector3.zero;
            float minNonOverlappingArea = float.MaxValue;
            foreach (ContextMenuOpenDirection enumVal in Enum.GetValues(typeof(ContextMenuOpenDirection)))
            {
                Vector2 offsetByDirection = GetOffsetByDirection(enumVal, offsetFromTarget);
                Vector3 currentPosition = position + new Vector3(offsetByDirection.x, offsetByDirection.y, 0);
                float nonOverlappingArea = CalculateNonOverlappingArea(overlapRect ?? viewRectTransform.rect, GetProjectedRect(currentPosition));
                if (nonOverlappingArea < minNonOverlappingArea)
                {
                    newPosition = currentPosition;
                    minNonOverlappingArea = nonOverlappingArea;
                }
            }

            return newPosition;
        }

        private float CalculateNonOverlappingArea(Rect rect1, Rect rect2)
        {
            float area1 = rect1.width * rect1.height;
            float area2 = rect2.width * rect2.height;

            Rect intersection = Rect.MinMaxRect(
                Mathf.Max(rect1.xMin, rect2.xMin),
                Mathf.Max(rect1.yMin, rect2.yMin),
                Mathf.Min(rect1.xMax, rect2.xMax),
                Mathf.Min(rect1.yMax, rect2.yMax)
            );

            float intersectionArea = 0;

            if (intersection is { width: > 0, height: > 0 })
                intersectionArea = intersection.width * intersection.height;

            return area1 + area2 - intersectionArea;
        }

        private Rect GetProjectedRect(Vector3 newPosition)
        {
            Vector3 originalPosition = viewInstance!.ControlsContainer.localPosition;
            viewInstance!.ControlsContainer.localPosition = newPosition;
            Rect rect = GetWorldRect(viewInstance!.ControlsContainer);
            viewInstance!.ControlsContainer.localPosition = originalPosition;

            return rect;
        }

        private static Rect GetWorldRect(RectTransform rectTransform)
        {
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            Vector2 min = corners[0];
            Vector2 max = corners[2];
            Vector2 size = max - min;
            return new Rect(min, size);
        }

        protected override void OnViewClose() =>
            controlsPoolManager.ReleaseAllCurrentControls();

        private void TriggerContextMenuClose() => isClosing = true;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(UniTask.WaitUntil(() => isClosing, cancellationToken: ct));
    }
}
