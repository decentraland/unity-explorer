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

            // Get base position for initial direction
            Vector3 anchoredPosition = GetPositionForDirection(initialDirection, position);
            Vector2 offsetByDirection = GetOffsetByDirection(initialDirection, offsetFromTarget);
            Vector3 basePosition = anchoredPosition + new Vector3(offsetByDirection.x, offsetByDirection.y, 0);
            
            // Apply container adjustments
            Vector3 adjustedBasePosition = ApplyContainerAdjustments(basePosition, initialDirection);
            
            // For debugging all directions
            Debug.Log($"[ContextMenu] Direction {initialDirection}: anchoredPosition={anchoredPosition}, " +
                     $"offsetByDirection={offsetByDirection}, basePosition={basePosition}, " +
                     $"adjustedBasePosition={adjustedBasePosition}");
            
            // Get boundary rect we need to stay within
            Rect boundaryRect = overlapRect ?? backgroundWorldRect;
            
            // Check if menu is within bounds with the initial direction
            Rect menuRect = GetProjectedRect(adjustedBasePosition);
            
            // Check if menu is fully contained within boundary
            bool isWithinBounds = IsRectContained(boundaryRect, menuRect);
            Debug.Log($"[ContextMenu] Initial position is within bounds: {isWithinBounds}");
            
            if (isWithinBounds)
            {
                Debug.Log($"[ContextMenu] Using initial position as it's within bounds: {adjustedBasePosition}");
                return adjustedBasePosition;
            }
            
            // Try to adjust the position to fit within bounds while maintaining the original direction
            Vector3 adjustedPosition = AdjustPositionToFitBounds(adjustedBasePosition, boundaryRect);
            Rect adjustedMenuRect = GetProjectedRect(adjustedPosition);
            bool adjustedIsWithinBounds = IsRectContained(boundaryRect, adjustedMenuRect);
            
            Debug.Log($"[ContextMenu] After boundary adjustment: position={adjustedPosition}, within bounds: {adjustedIsWithinBounds}");
            
            if (adjustedIsWithinBounds)
            {
                Debug.Log($"[ContextMenu] Using adjusted position: {adjustedPosition}");
                return adjustedPosition;
            }
            
            // If we couldn't adjust the position to fit, try different directions
            Debug.Log($"[ContextMenu] Could not adjust to fit within bounds, trying alternative directions...");
            
            // Define fallback directions by category
            ContextMenuOpenDirection[] fallbackOrder = GetFallbackDirections(initialDirection);
            Debug.Log($"[ContextMenu] Fallback order: {string.Join(", ", fallbackOrder)}");

            // Track the best position (one with least amount outside bounds)
            float bestOutOfBoundsArea = CalculateOutOfBoundsArea(boundaryRect, adjustedMenuRect);
            Vector3 bestPosition = adjustedPosition; // Start with our adjusted position as fallback

            // Try each direction in the fallback sequence
            foreach (var currentDirection in fallbackOrder)
            {
                // Skip the initial direction as we already tried it
                if (currentDirection == initialDirection)
                    continue;
                    
                Vector3 currentAnchoredPosition = GetPositionForDirection(currentDirection, position);
                Debug.Log($"[ContextMenu] Trying direction: {currentDirection}, Position after direction adjustment: {currentAnchoredPosition}");

                // Try each offset direction with the current anchor direction
                foreach (ContextMenuOpenDirection offsetDirection in openDirections)
                {
                    Vector2 currentOffsetByDirection = GetOffsetByDirection(offsetDirection, offsetFromTarget);
                    Vector3 currentPosition = currentAnchoredPosition + new Vector3(currentOffsetByDirection.x, currentOffsetByDirection.y, 0);
                    
                    // Apply container adjustments
                    Vector3 adjustedCurrentPosition = ApplyContainerAdjustments(currentPosition, offsetDirection);
                    
                    Debug.Log($"[ContextMenu] Direction {offsetDirection}: adjustedCurrentPosition={adjustedCurrentPosition}");

                    // Try to adjust this position to fit bounds
                    Vector3 boundaryAdjustedPosition = AdjustPositionToFitBounds(adjustedCurrentPosition, boundaryRect);
                    Rect currentMenuRect = GetProjectedRect(boundaryAdjustedPosition);
                    bool currentIsWithinBounds = IsRectContained(boundaryRect, currentMenuRect);
                    
                    Debug.Log($"[ContextMenu] Position after adjustment: {boundaryAdjustedPosition}, within bounds: {currentIsWithinBounds}");

                    // If this position is within bounds, use it immediately
                    if (currentIsWithinBounds)
                    {
                        Debug.Log($"[ContextMenu] Found position within bounds! Using: {boundaryAdjustedPosition}");
                        return boundaryAdjustedPosition;
                    }
                    
                    // Otherwise, track the position with least area outside bounds
                    float currentOutOfBoundsArea = CalculateOutOfBoundsArea(boundaryRect, currentMenuRect);
                    if (currentOutOfBoundsArea < bestOutOfBoundsArea)
                    {
                        bestPosition = boundaryAdjustedPosition;
                        bestOutOfBoundsArea = currentOutOfBoundsArea;
                        Debug.Log($"[ContextMenu] New best position found: {bestPosition} with out-of-bounds area: {bestOutOfBoundsArea}");
                    }
                }
            }

            Debug.Log($"[ContextMenu] Final best position: {bestPosition} with out-of-bounds area: {bestOutOfBoundsArea}");
            return bestPosition;
        }
        
        private bool IsRectContained(Rect container, Rect rect)
        {
            // Check if rect is completely inside container
            return rect.xMin >= container.xMin && 
                   rect.xMax <= container.xMax && 
                   rect.yMin >= container.yMin && 
                   rect.yMax <= container.yMax;
        }
        
        private float CalculateOutOfBoundsArea(Rect container, Rect rect)
        {
            // Calculate how much of the rect is outside the container
            float outOfBoundsWidth = 0;
            float outOfBoundsHeight = 0;
            
            // Check horizontal overflow
            if (rect.xMin < container.xMin)
                outOfBoundsWidth += container.xMin - rect.xMin;
            if (rect.xMax > container.xMax)
                outOfBoundsWidth += rect.xMax - container.xMax;
                
            // Check vertical overflow
            if (rect.yMin < container.yMin)
                outOfBoundsHeight += container.yMin - rect.yMin;
            if (rect.yMax > container.yMax)
                outOfBoundsHeight += rect.yMax - container.yMax;
                
            // Calculate total area outside bounds
            return outOfBoundsWidth * rect.height + outOfBoundsHeight * rect.width - (outOfBoundsWidth * outOfBoundsHeight);
        }
        
        private Vector3 AdjustPositionToFitBounds(Vector3 position, Rect boundaryRect)
        {
            Vector3 adjustedPosition = position;
            Rect menuRect = GetProjectedRect(position);
            
            // Adjust horizontal position to fit within bounds
            if (menuRect.xMin < boundaryRect.xMin)
            {
                // Move right to fit left edge
                float adjustment = boundaryRect.xMin - menuRect.xMin;
                adjustedPosition.x += adjustment;
                Debug.Log($"[ContextMenu] Adjusting right by {adjustment} to fit left boundary");
            }
            else if (menuRect.xMax > boundaryRect.xMax)
            {
                // Move left to fit right edge
                float adjustment = menuRect.xMax - boundaryRect.xMax;
                adjustedPosition.x -= adjustment;
                Debug.Log($"[ContextMenu] Adjusting left by {adjustment} to fit right boundary");
            }
            
            // Adjust vertical position to fit within bounds
            if (menuRect.yMin < boundaryRect.yMin)
            {
                // Move up to fit bottom edge
                float adjustment = boundaryRect.yMin - menuRect.yMin;
                adjustedPosition.y += adjustment;
                Debug.Log($"[ContextMenu] Adjusting up by {adjustment} to fit bottom boundary");
            }
            else if (menuRect.yMax > boundaryRect.yMax)
            {
                // Move down to fit top edge
                float adjustment = menuRect.yMax - boundaryRect.yMax;
                adjustedPosition.y -= adjustment;
                Debug.Log($"[ContextMenu] Adjusting down by {adjustment} to fit top boundary");
            }
            
            return adjustedPosition;
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
            float halfWidth = viewInstance!.ControlsContainer.rect.width / 2;
            float halfHeight = viewInstance!.ControlsContainer.rect.height / 2;
            Vector3 result = position;
            
            // Apply horizontal offset based on direction
            switch (direction)
            {
                case ContextMenuOpenDirection.TOP_LEFT:
                case ContextMenuOpenDirection.BOTTOM_LEFT:
                case ContextMenuOpenDirection.CENTER_LEFT:
                    // For LEFT positions, we want the right edge to be at the anchor point
                    // So we move left by half width
                    result.x -= halfWidth;
                    break;
                case ContextMenuOpenDirection.TOP_RIGHT:
                case ContextMenuOpenDirection.BOTTOM_RIGHT:
                case ContextMenuOpenDirection.CENTER_RIGHT:
                    // For RIGHT positions, we want the left edge to be at the anchor point
                    // So we move right by half width
                    result.x += halfWidth;
                    break;
            }
            
            // Apply vertical offset based on direction
            switch (direction)
            {
                case ContextMenuOpenDirection.TOP_LEFT:
                case ContextMenuOpenDirection.TOP_RIGHT:
                    // For TOP positions, we want the bottom edge to be at the anchor point
                    // So we move up by half height
                    result.y += halfHeight;
                    break;
                case ContextMenuOpenDirection.BOTTOM_LEFT:
                case ContextMenuOpenDirection.BOTTOM_RIGHT:
                    // For BOTTOM positions, we want the top edge to be at the anchor point
                    // So we move down by half height
                    result.y -= halfHeight;
                    break;
                case ContextMenuOpenDirection.CENTER_LEFT:
                case ContextMenuOpenDirection.CENTER_RIGHT:
                    // For CENTER positions, we want the vertical center to be at the anchor point
                    // No vertical adjustment needed
                    break;
            }
            
            Debug.Log($"[ContextMenu] GetPositionForDirection {direction}: {position} -> {result}");
            return result;
        }

        private float CalculateIntersectionArea(Rect rect1, Rect rect2)
        {
            // Calculate the intersection
            Rect intersection = CalculateIntersection(rect1, rect2);
            
            // If there's no intersection, return 0
            if (intersection.width <= 0 || intersection.height <= 0)
                return 0;
                
            // Return the intersection area in square pixels
            return intersection.width * intersection.height;
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

        private Rect CalculateIntersection(Rect rect1, Rect rect2)
        {
            return Rect.MinMaxRect(
                Mathf.Max(rect1.xMin, rect2.xMin),
                Mathf.Max(rect1.yMin, rect2.yMin),
                Mathf.Min(rect1.xMax, rect2.xMax),
                Mathf.Min(rect1.yMax, rect2.yMax)
            );
        }
        
        private Vector3 ApplyContainerAdjustments(Vector3 position, ContextMenuOpenDirection direction)
        {
            // We already did the positioning in GetPositionForDirection, so we don't need any further adjustments
            // This method is kept for compatibility
            return position;
        }
    }
}


