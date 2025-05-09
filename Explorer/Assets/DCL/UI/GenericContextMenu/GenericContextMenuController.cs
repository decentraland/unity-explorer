using Cysharp.Threading.Tasks;
using DCL.UI.GenericContextMenu.Controls;
using DCL.UI.GenericContextMenu.Controls.Configs;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.UI.GenericContextMenu
{
    public enum ContextMenuOpenDirection
    {
        BOTTOM_RIGHT,
        TOP_RIGHT,
        CENTER_RIGHT,
        BOTTOM_LEFT,
        TOP_LEFT,
        CENTER_LEFT,
    }

    public class GenericContextMenuController : ControllerBase<GenericContextMenuView, GenericContextMenuParameter>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private readonly ControlsPoolManager controlsPoolManager;
        private readonly Vector3[] worldRectCorners = new Vector3[4];
        private readonly ContextMenuOpenDirection[] openDirections = EnumUtils.Values<ContextMenuOpenDirection>();

        private RectTransform viewRectTransform;
        private Rect backgroundWorldRect;
        private UniTaskCompletionSource internalCloseTask;

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
            backgroundWorldRect = GetWorldRect(viewInstance!.BackgroundCloseButton.GetComponent<RectTransform>());
        }

        protected override void OnBeforeViewShow()
        {
            internalCloseTask = new UniTaskCompletionSource();

            ConfigureContextMenu();
        }

        protected override void OnViewShow()
        {
            base.OnViewShow();
            inputData.ActionOnShow?.Invoke();
        }

        private void ConfigureContextMenu()
        {
            float totalHeight = 0;

            for (var i = 0; i < inputData.Config.contextMenuSettings.Count; i++)
            {
                GenericContextMenuElement config = inputData.Config.contextMenuSettings[i];

                if (!config.Enabled) continue;

                GenericContextMenuComponentBase component = controlsPoolManager.GetContextMenuComponent(config.setting, i);

                component.RegisterCloseListener(TriggerContextMenuClose);

                totalHeight += component!.RectTransformComponent.rect.height;
            }

            viewInstance!.ControlsLayoutGroup.spacing = inputData.Config.elementsSpacing;
            viewInstance!.ControlsLayoutGroup.padding = inputData.Config.verticalLayoutPadding;
            viewInstance.ControlsContainer.sizeDelta = new Vector2(inputData.Config.width,
                totalHeight
                + viewInstance!.ControlsLayoutGroup.padding.bottom
                + viewInstance!.ControlsLayoutGroup.padding.top
                + (viewInstance!.ControlsLayoutGroup.spacing * (inputData.Config.contextMenuSettings.Count - 1)));

            viewInstance!.ControlsContainer.localPosition = GetControlsPosition(inputData.AnchorPosition, inputData.Config.offsetFromTarget, inputData.OverlapRect, inputData.Config.anchorPoint);
        }

        private Vector2 GetOffsetByDirection(ContextMenuOpenDirection direction, Vector2 offsetFromTarget)
        {
            return direction switch
            {
                ContextMenuOpenDirection.BOTTOM_RIGHT => offsetFromTarget,
                ContextMenuOpenDirection.BOTTOM_LEFT => new Vector2(-offsetFromTarget.x, offsetFromTarget.y),
                ContextMenuOpenDirection.TOP_RIGHT => new Vector2(offsetFromTarget.x, -offsetFromTarget.y),
                ContextMenuOpenDirection.TOP_LEFT => new Vector2(-offsetFromTarget.x, -offsetFromTarget.y),
                ContextMenuOpenDirection.CENTER_RIGHT => new Vector2(offsetFromTarget.x, 0),
                ContextMenuOpenDirection.CENTER_LEFT => new Vector2(-offsetFromTarget.x, 0),
                _ => Vector2.zero
            };
        }

        private Vector3 GetControlsPosition(Vector2 anchorPosition, Vector2 offsetFromTarget, Rect? overlapRect, ContextMenuOpenDirection initialDirection = ContextMenuOpenDirection.TOP_LEFT, bool exactPosition = false)
        {
            Vector3 position = viewRectTransform.InverseTransformPoint(anchorPosition);
            Debug.Log($"[ContextMenu] Initial position: {position}, Requested direction: {initialDirection}");

            // Define fallback directions by category
            ContextMenuOpenDirection[] fallbackOrder = GetFallbackDirections(initialDirection);
            Debug.Log($"[ContextMenu] Fallback order: {string.Join(", ", fallbackOrder)}");

            Vector3 bestPosition = Vector3.zero;
            float bestNonOverlappingArea = float.MaxValue;

            // Try each direction in the fallback sequence
            foreach (var currentDirection in fallbackOrder)
            {
                Vector3 anchoredPosition = GetPositionForDirection(currentDirection, position);
                Debug.Log($"[ContextMenu] Trying direction: {currentDirection}, Position after direction adjustment: {anchoredPosition}");

                // Try each offset direction with the current anchor direction
                foreach (ContextMenuOpenDirection offsetDirection in openDirections)
                {
                    Vector2 offsetByDirection = GetOffsetByDirection(offsetDirection, offsetFromTarget);
                    Vector3 currentPosition = anchoredPosition + new Vector3(offsetByDirection.x, offsetByDirection.y, 0);
                    Debug.Log($"[ContextMenu] Trying offset direction: {offsetDirection}, Position after offset: {currentPosition}");

                    // Apply container width adjustment based on direction
                    if (offsetDirection == ContextMenuOpenDirection.BOTTOM_LEFT || 
                        offsetDirection == ContextMenuOpenDirection.TOP_LEFT ||
                        offsetDirection == ContextMenuOpenDirection.CENTER_LEFT)
                    {
                        currentPosition.x -= viewInstance!.ControlsContainer.rect.width;
                    }

                    // Apply container height adjustment based on direction
                    if (offsetDirection == ContextMenuOpenDirection.TOP_RIGHT || 
                        offsetDirection == ContextMenuOpenDirection.TOP_LEFT)
                    {
                        currentPosition.y += viewInstance!.ControlsContainer.rect.height;
                    }
                    // CENTER positions need half height adjustment
                    else if (offsetDirection == ContextMenuOpenDirection.CENTER_RIGHT ||
                             offsetDirection == ContextMenuOpenDirection.CENTER_LEFT)
                    {
                        currentPosition.y += viewInstance!.ControlsContainer.rect.height / 2;
                    }

                    Debug.Log($"[ContextMenu] Position after container size adjustments: {currentPosition}");

                    float nonOverlappingArea = CalculateNonOverlappingArea(overlapRect ?? backgroundWorldRect, GetProjectedRect(currentPosition));
                    Debug.Log($"[ContextMenu] Non-overlapping area: {nonOverlappingArea}");

                    if (nonOverlappingArea < bestNonOverlappingArea)
                    {
                        bestPosition = currentPosition;
                        bestNonOverlappingArea = nonOverlappingArea;
                        Debug.Log($"[ContextMenu] New best position found: {bestPosition} with area: {bestNonOverlappingArea}");
                    }

                    // If we found a position with very minimal overlap, we can return early
                    if (nonOverlappingArea < 0.1f)
                    {
                        Debug.Log($"[ContextMenu] Found excellent position with minimal overlap! Early exit with position: {currentPosition}");
                        return currentPosition;
                    }
                }
            }

            Debug.Log($"[ContextMenu] Final best position: {bestPosition} with non-overlapping area: {bestNonOverlappingArea}");
            return bestPosition;
        }

        private ContextMenuOpenDirection[] GetFallbackDirections(ContextMenuOpenDirection initialDirection)
        {
            // Group directions by vertical position
            ContextMenuOpenDirection[] topPoints = {
                ContextMenuOpenDirection.TOP_LEFT,
                ContextMenuOpenDirection.TOP_RIGHT
            };

            ContextMenuOpenDirection[] centerPoints = {
                ContextMenuOpenDirection.CENTER_LEFT,
                ContextMenuOpenDirection.CENTER_RIGHT
            };

            ContextMenuOpenDirection[] bottomPoints = {
                ContextMenuOpenDirection.BOTTOM_LEFT,
                ContextMenuOpenDirection.BOTTOM_RIGHT
            };

            // Start with the initial direction
            var result = new ContextMenuOpenDirection[5];
            result[0] = initialDirection;

            // Determine fallback sequence based on the initial direction
            if (Array.IndexOf(topPoints, initialDirection) >= 0)
            {
                // If we started with a TOP point, try CENTER then BOTTOM
                result[1] = ContextMenuOpenDirection.CENTER_LEFT;
                result[2] = ContextMenuOpenDirection.CENTER_RIGHT;
                result[3] = ContextMenuOpenDirection.BOTTOM_LEFT;
                result[4] = ContextMenuOpenDirection.BOTTOM_RIGHT;
            }
            else if (Array.IndexOf(centerPoints, initialDirection) >= 0)
            {
                // If we started with a CENTER point, try TOP then BOTTOM
                result[1] = ContextMenuOpenDirection.TOP_LEFT;
                result[2] = ContextMenuOpenDirection.TOP_RIGHT;
                result[3] = ContextMenuOpenDirection.BOTTOM_LEFT;
                result[4] = ContextMenuOpenDirection.BOTTOM_RIGHT;
            }
            else
            {
                // If we started with a BOTTOM point, try CENTER then TOP
                result[1] = ContextMenuOpenDirection.CENTER_LEFT;
                result[2] = ContextMenuOpenDirection.CENTER_RIGHT;
                result[3] = ContextMenuOpenDirection.TOP_LEFT;
                result[4] = ContextMenuOpenDirection.TOP_RIGHT;
            }

            return result;
        }

        private Vector3 GetPositionForDirection(ContextMenuOpenDirection direction, Vector3 position)
        {
            switch (direction)
            {
                case ContextMenuOpenDirection.TOP_LEFT:
                    position.y -= viewInstance!.ControlsContainer.rect.height / 2;
                    position.x += viewInstance!.ControlsContainer.rect.width / 2;
                    break;
                case ContextMenuOpenDirection.TOP_RIGHT:
                    position.y -= viewInstance!.ControlsContainer.rect.height / 2;
                    position.x -= viewInstance!.ControlsContainer.rect.width / 2;
                    break;
                case ContextMenuOpenDirection.BOTTOM_LEFT:
                    position.y += viewInstance!.ControlsContainer.rect.height / 2;
                    position.x += viewInstance!.ControlsContainer.rect.width / 2;
                    break;
                case ContextMenuOpenDirection.BOTTOM_RIGHT:
                    position.y += viewInstance!.ControlsContainer.rect.height / 2;
                    position.x -= viewInstance!.ControlsContainer.rect.width / 2;
                    break;
                case ContextMenuOpenDirection.CENTER_LEFT:
                    position.x += viewInstance!.ControlsContainer.rect.width / 2;
                    break;
                case ContextMenuOpenDirection.CENTER_RIGHT:
                    position.x -= viewInstance!.ControlsContainer.rect.width / 2;
                    break;
            }

            return position;
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

            Debug.Log($"[ContextMenu] Rect1: ({rect1.xMin},{rect1.yMin},{rect1.xMax},{rect1.yMax}), Rect2: ({rect2.xMin},{rect2.yMin},{rect2.xMax},{rect2.yMax})");

            float intersectionArea = 0;

            if (intersection is { width: > 0, height: > 0 })
            {
                intersectionArea = intersection.width * intersection.height;
                Debug.Log($"[ContextMenu] Intersection: ({intersection.xMin},{intersection.yMin},{intersection.xMax},{intersection.yMax}), Area: {intersectionArea}");
            }
            else
            {
                Debug.Log("[ContextMenu] No intersection between rects");
            }

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

        private Rect GetWorldRect(RectTransform rectTransform)
        {
            rectTransform.GetWorldCorners(worldRectCorners);
            Vector2 min = worldRectCorners[0];
            Vector2 max = worldRectCorners[2];
            Vector2 size = max - min;
            return new Rect(min, size);
        }

        protected override void OnViewClose()
        {
            controlsPoolManager.ReleaseAllCurrentControls();
            inputData.ActionOnHide?.Invoke();
        }

        private void TriggerContextMenuClose() => internalCloseTask.TrySetResult();

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            UniTask inputCloseTask = inputData.CloseTask ?? UniTask.Never(ct);
            return UniTask.WhenAny(internalCloseTask.Task, inputCloseTask, viewInstance!.BackgroundCloseButton.Button.OnClickAsync(ct));
        }
    }
}
