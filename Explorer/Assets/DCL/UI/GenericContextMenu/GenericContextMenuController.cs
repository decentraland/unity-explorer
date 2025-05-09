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

            // Get base position for the initial direction
            Vector3 basePosition = GetPositionForDirection(initialDirection, position);
            Vector2 offsetByDirection = GetOffsetByDirection(initialDirection, offsetFromTarget);
            Vector3 initialPosition = basePosition + new Vector3(offsetByDirection.x, offsetByDirection.y, 0);
            Vector3 adjustedInitialPosition = ApplyContainerAdjustments(initialPosition, initialDirection);
            
            // Get boundary rect we need to stay within
            Rect boundaryRect = overlapRect ?? backgroundWorldRect;
            
            // Calculate menu dimensions
            float menuWidth = viewInstance!.ControlsContainer.rect.width;
            float menuHeight = viewInstance!.ControlsContainer.rect.height;
            
            // Project the menu rectangle at the initial position
            Rect menuRect = GetProjectedRect(adjustedInitialPosition);
            
            // Calculate how much the menu would go out of bounds (as percentage of menu size)
            float outOfBoundsPercentTop = 0;
            float outOfBoundsPercentBottom = 0;
            float outOfBoundsPercentRight = 0;
            float outOfBoundsPercentLeft = 0;
            
            if (menuRect.yMax > boundaryRect.yMax)
            {
                float overflow = menuRect.yMax - boundaryRect.yMax;
                outOfBoundsPercentTop = overflow / menuHeight;
            }
            
            if (menuRect.yMin < boundaryRect.yMin)
            {
                float overflow = boundaryRect.yMin - menuRect.yMin;
                outOfBoundsPercentBottom = overflow / menuHeight;
            }
            
            if (menuRect.xMax > boundaryRect.xMax)
            {
                float overflow = menuRect.xMax - boundaryRect.xMax;
                outOfBoundsPercentRight = overflow / menuWidth;
            }
            
            if (menuRect.xMin < boundaryRect.xMin)
            {
                float overflow = boundaryRect.xMin - menuRect.xMin;
                outOfBoundsPercentLeft = overflow / menuWidth;
            }
            
            // Calculate total percentage out of bounds
            float totalOutOfBoundsPercent = outOfBoundsPercentTop + outOfBoundsPercentBottom + 
                                           outOfBoundsPercentRight + outOfBoundsPercentLeft;
            
            Debug.Log($"[ContextMenu] Out of bounds analysis: Top={outOfBoundsPercentTop:P2}, Bottom={outOfBoundsPercentBottom:P2}, " +
                      $"Right={outOfBoundsPercentRight:P2}, Left={outOfBoundsPercentLeft:P2}, Total={totalOutOfBoundsPercent:P2}");
            
            // Threshold for minimum adjustment (1% is often visual noise)
            const float MINIMAL_ADJUSTMENT_THRESHOLD = 0.01f;
            
            // Check if the menu is already well-positioned or only minimally out of bounds
            if (totalOutOfBoundsPercent < MINIMAL_ADJUSTMENT_THRESHOLD)
            {
                Debug.Log($"[ContextMenu] Only {totalOutOfBoundsPercent:P2} out of bounds, keeping initial position: {adjustedInitialPosition}");
                return adjustedInitialPosition;
            }
            
            // Check if the menu would go out of bounds in different directions
            bool outOfBoundsOnRight = outOfBoundsPercentRight > 0;
            bool outOfBoundsOnLeft = outOfBoundsPercentLeft > 0;
            bool outOfBoundsOnTop = outOfBoundsPercentTop > 0;
            bool outOfBoundsOnBottom = outOfBoundsPercentBottom > 0;
            
            Debug.Log($"[ContextMenu] Boundary checks: Right={outOfBoundsOnRight}, Left={outOfBoundsOnLeft}, " +
                      $"Top={outOfBoundsOnTop}, Bottom={outOfBoundsOnBottom}");
            
            // Determine if we need to switch sides based on boundary checks
            HorizontalPosition initialHorizontal = GetHorizontalPosition(initialDirection);
            VerticalPosition initialVertical = GetVerticalPosition(initialDirection);
            
            // If we would go out of bounds, try the opposite side immediately
            ContextMenuOpenDirection smartDirection = initialDirection;
            
            // Check horizontal constraints
            if (initialHorizontal == HorizontalPosition.RIGHT && outOfBoundsOnRight)
            {
                // Switch to LEFT if it would go out of bounds on the RIGHT
                smartDirection = GetOppositeHorizontalDirection(initialDirection);
                Debug.Log($"[ContextMenu] Would go out of bounds on right, switching from {initialDirection} to {smartDirection}");
            }
            else if (initialHorizontal == HorizontalPosition.LEFT && outOfBoundsOnLeft)
            {
                // Switch to RIGHT if it would go out of bounds on the LEFT
                smartDirection = GetOppositeHorizontalDirection(initialDirection);
                Debug.Log($"[ContextMenu] Would go out of bounds on left, switching from {initialDirection} to {smartDirection}");
            }
            
            // Check vertical constraints - apply to potentially already horizontally adjusted direction
            if (initialVertical == VerticalPosition.TOP && outOfBoundsOnTop)
            {
                // Switch to BOTTOM if it would go out of bounds on the TOP
                smartDirection = GetOppositeVerticalDirection(smartDirection);
                Debug.Log($"[ContextMenu] Would go out of bounds on top, switching to {smartDirection}");
            }
            else if (initialVertical == VerticalPosition.BOTTOM && outOfBoundsOnBottom)
            {
                // Switch to TOP if it would go out of bounds on the BOTTOM
                smartDirection = GetOppositeVerticalDirection(smartDirection);
                Debug.Log($"[ContextMenu] Would go out of bounds on bottom, switching to {smartDirection}");
            }
            
            // Proceed with the smart direction (either original or adjusted)
            // Get base position for the smart direction
            Vector3 anchoredPosition = GetPositionForDirection(smartDirection, position);
            Vector2 offsetBySmartDirection = GetOffsetByDirection(smartDirection, offsetFromTarget);
            Vector3 baseSmartPosition = anchoredPosition + new Vector3(offsetBySmartDirection.x, offsetBySmartDirection.y, 0);
            
            // For debugging all directions
            Debug.Log($"[ContextMenu] Using direction {smartDirection}: anchoredPosition={anchoredPosition}, " +
                     $"offsetByDirection={offsetBySmartDirection}, basePosition={baseSmartPosition}");
            
            // Apply container adjustments
            Vector3 adjustedBasePosition = ApplyContainerAdjustments(baseSmartPosition, smartDirection);
            
            // Check if menu is within bounds with the smart direction
            Rect smartMenuRect = GetProjectedRect(adjustedBasePosition);
            
            // Check if menu is fully contained within boundary
            bool isWithinBounds = IsRectContained(boundaryRect, smartMenuRect);
            Debug.Log($"[ContextMenu] Smart position is within bounds: {isWithinBounds}");
            
            if (isWithinBounds)
            {
                Debug.Log($"[ContextMenu] Using smart position as it's within bounds: {adjustedBasePosition}");
                return adjustedBasePosition;
            }
            
            // Calculate how much the smart position is out of bounds (as percentage)
            float smartOutOfBoundsPercent = CalculateOutOfBoundsPercent(boundaryRect, smartMenuRect);
            
            // Try to adjust the position to fit within bounds while maintaining the smart direction
            Vector3 adjustedPosition = AdjustPositionToFitBounds(adjustedBasePosition, boundaryRect);
            Rect adjustedMenuRect = GetProjectedRect(adjustedPosition);
            bool adjustedIsWithinBounds = IsRectContained(boundaryRect, adjustedMenuRect);
            float adjustedOutOfBoundsPercent = adjustedIsWithinBounds ? 0 : CalculateOutOfBoundsPercent(boundaryRect, adjustedMenuRect);
            
            Debug.Log($"[ContextMenu] After boundary adjustment: position={adjustedPosition}, within bounds: {adjustedIsWithinBounds}, " +
                      $"out of bounds: {adjustedOutOfBoundsPercent:P2} (was {smartOutOfBoundsPercent:P2})");
            
            // If the adjusted position is within bounds, use it
            if (adjustedIsWithinBounds)
            {
                Debug.Log($"[ContextMenu] Adjusted position is within bounds, using: {adjustedPosition}");
                return adjustedPosition;
            }
            
            // If we still couldn't adjust the position to fit, try different directions
            Debug.Log($"[ContextMenu] Could not adjust to fit within bounds, trying alternative directions...");
            
            // Define fallback directions, starting with the smart direction
            ContextMenuOpenDirection[] fallbackOrder = GetFallbackDirections(smartDirection);
            Debug.Log($"[ContextMenu] Fallback order: {string.Join(", ", fallbackOrder)}");

            // Track the best position (one with least amount outside bounds)
            float bestOutOfBoundsPercent = adjustedOutOfBoundsPercent;
            Vector3 bestPosition = adjustedPosition; // Start with our adjusted position as fallback
            bool foundPerfectPosition = false; // Track if we found a perfect (within bounds) position

            // Try each direction in the fallback sequence
            foreach (var currentDirection in fallbackOrder)
            {
                // Skip the smart direction as we already tried it
                if (currentDirection == smartDirection)
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
                    float currentOutOfBoundsPercent = CalculateOutOfBoundsPercent(boundaryRect, currentMenuRect);
                    if (currentOutOfBoundsPercent < bestOutOfBoundsPercent)
                    {
                        bestPosition = boundaryAdjustedPosition;
                        bestOutOfBoundsPercent = currentOutOfBoundsPercent;
                        Debug.Log($"[ContextMenu] New best position found: {bestPosition} with out-of-bounds: {bestOutOfBoundsPercent:P2}");
                    }
                }
            }

            // Only accept an out-of-bounds position if we've tried everything else
            Debug.Log($"[ContextMenu] Final best position: {bestPosition} with out-of-bounds: {bestOutOfBoundsPercent:P2}");
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
            // Split direction into horizontal and vertical components
            HorizontalPosition initialHorizontal = GetHorizontalPosition(initialDirection);
            VerticalPosition initialVertical = GetVerticalPosition(initialDirection);
            
            // Check if the menu would go out of bounds vertically
            float outOfBoundsPercentTop = 0;
            float outOfBoundsPercentBottom = 0;
            // Check horizontal boundaries too
            float outOfBoundsPercentRight = 0;
            float outOfBoundsPercentLeft = 0;
            
            // Get menu dimensions
            float menuHeight = viewInstance!.ControlsContainer.rect.height;
            float menuWidth = viewInstance!.ControlsContainer.rect.width;
            
            // Get anchor position in world space
            Vector2 anchorPosition = inputData.AnchorPosition;
            
            // Get boundary rect
            Rect boundaryRect = inputData.OverlapRect ?? backgroundWorldRect;
            
            // Check if the menu would go out of bounds with current position
            Vector3 menuPosition = GetPositionForDirection(initialDirection, viewRectTransform.InverseTransformPoint(anchorPosition));
            menuPosition = ApplyContainerAdjustments(menuPosition, initialDirection);
            Rect menuRect = GetProjectedRect(menuPosition);
            
            // Calculate how much the menu would go out of bounds
            if (menuRect.yMax > boundaryRect.yMax)
            {
                float overflow = menuRect.yMax - boundaryRect.yMax;
                outOfBoundsPercentTop = overflow / menuHeight;
                Debug.Log($"[ContextMenu] Out of bounds on top: {outOfBoundsPercentTop:P2}");
            }
            
            if (menuRect.yMin < boundaryRect.yMin)
            {
                float overflow = boundaryRect.yMin - menuRect.yMin;
                outOfBoundsPercentBottom = overflow / menuHeight;
                Debug.Log($"[ContextMenu] Out of bounds on bottom: {outOfBoundsPercentBottom:P2}");
            }
            
            if (menuRect.xMax > boundaryRect.xMax)
            {
                float overflow = menuRect.xMax - boundaryRect.xMax;
                outOfBoundsPercentRight = overflow / menuWidth;
                Debug.Log($"[ContextMenu] Out of bounds on right: {outOfBoundsPercentRight:P2}");
            }
            
            if (menuRect.xMin < boundaryRect.xMin)
            {
                float overflow = boundaryRect.xMin - menuRect.xMin;
                outOfBoundsPercentLeft = overflow / menuWidth;
                Debug.Log($"[ContextMenu] Out of bounds on left: {outOfBoundsPercentLeft:P2}");
            }
            
            // The threshold for when to skip CENTER positions (40%)
            const float SEVERE_BOUNDARY_VIOLATION_THRESHOLD = 0.4f;
            
            // Variables to track which positions we should avoid
            bool avoidTop = outOfBoundsPercentTop > 0;
            bool avoidBottom = outOfBoundsPercentBottom > 0;
            bool skipCenter = outOfBoundsPercentTop > SEVERE_BOUNDARY_VIOLATION_THRESHOLD || 
                               outOfBoundsPercentBottom > SEVERE_BOUNDARY_VIOLATION_THRESHOLD;
            
            // Avoid horizontal positions too
            bool avoidRight = outOfBoundsPercentRight > 0;
            bool avoidLeft = outOfBoundsPercentLeft > 0;
            
            Debug.Log($"[ContextMenu] Position constraints: avoidTop={avoidTop}, avoidBottom={avoidBottom}, " +
                      $"skipCenter={skipCenter}, avoidRight={avoidRight}, avoidLeft={avoidLeft}");
            
            // Create result array for all 6 directions with priority order
            var resultList = new System.Collections.Generic.List<ContextMenuOpenDirection>(6);
            
            // Always start with the initial direction unless it's clearly invalid
            if ((initialVertical == VerticalPosition.TOP && !avoidTop) ||
                (initialVertical == VerticalPosition.BOTTOM && !avoidBottom) ||
                (initialVertical == VerticalPosition.CENTER) ||
                (initialHorizontal == HorizontalPosition.LEFT && !avoidLeft) ||
                (initialHorizontal == HorizontalPosition.RIGHT && !avoidRight))
            {
                resultList.Add(initialDirection);
            }
            
            // First prioritize fixing the most severe boundary violation
            if (outOfBoundsPercentTop > outOfBoundsPercentBottom && outOfBoundsPercentTop > 0)
            {
                // Top violation is worse, prioritize bottom positions
                if (initialHorizontal == HorizontalPosition.LEFT)
                {
                    resultList.Add(ContextMenuOpenDirection.BOTTOM_LEFT);
                    if (!skipCenter && !avoidBottom) resultList.Add(ContextMenuOpenDirection.CENTER_LEFT);
                    
                    // Only if right side isn't problematic
                    if (!avoidRight)
                    {
                        resultList.Add(ContextMenuOpenDirection.BOTTOM_RIGHT);
                        if (!skipCenter && !avoidBottom) resultList.Add(ContextMenuOpenDirection.CENTER_RIGHT);
                    }
                }
                else // RIGHT
                {
                    resultList.Add(ContextMenuOpenDirection.BOTTOM_RIGHT);
                    if (!skipCenter && !avoidBottom) resultList.Add(ContextMenuOpenDirection.CENTER_RIGHT);
                    
                    // Only if left side isn't problematic
                    if (!avoidLeft)
                    {
                        resultList.Add(ContextMenuOpenDirection.BOTTOM_LEFT);
                        if (!skipCenter && !avoidBottom) resultList.Add(ContextMenuOpenDirection.CENTER_LEFT);
                    }
                }
                
                // Add top positions only as last resort and they're not in the list yet
                if (!resultList.Contains(ContextMenuOpenDirection.TOP_LEFT) && !avoidLeft) 
                    resultList.Add(ContextMenuOpenDirection.TOP_LEFT);
                if (!resultList.Contains(ContextMenuOpenDirection.TOP_RIGHT) && !avoidRight) 
                    resultList.Add(ContextMenuOpenDirection.TOP_RIGHT);
            }
            else if (outOfBoundsPercentBottom > 0)
            {
                // Bottom violation is worse or only violation, prioritize top positions
                if (initialHorizontal == HorizontalPosition.LEFT)
                {
                    resultList.Add(ContextMenuOpenDirection.TOP_LEFT);
                    if (!skipCenter && !avoidTop) resultList.Add(ContextMenuOpenDirection.CENTER_LEFT);
                    
                    // Only if right side isn't problematic
                    if (!avoidRight)
                    {
                        resultList.Add(ContextMenuOpenDirection.TOP_RIGHT);
                        if (!skipCenter && !avoidTop) resultList.Add(ContextMenuOpenDirection.CENTER_RIGHT);
                    }
                }
                else // RIGHT
                {
                    resultList.Add(ContextMenuOpenDirection.TOP_RIGHT);
                    if (!skipCenter && !avoidTop) resultList.Add(ContextMenuOpenDirection.CENTER_RIGHT);
                    
                    // Only if left side isn't problematic
                    if (!avoidLeft)
                    {
                        resultList.Add(ContextMenuOpenDirection.TOP_LEFT);
                        if (!skipCenter && !avoidTop) resultList.Add(ContextMenuOpenDirection.CENTER_LEFT);
                    }
                }
                
                // Add bottom positions only as last resort and they're not in the list yet
                if (!resultList.Contains(ContextMenuOpenDirection.BOTTOM_LEFT) && !avoidLeft) 
                    resultList.Add(ContextMenuOpenDirection.BOTTOM_LEFT);
                if (!resultList.Contains(ContextMenuOpenDirection.BOTTOM_RIGHT) && !avoidRight) 
                    resultList.Add(ContextMenuOpenDirection.BOTTOM_RIGHT);
            }
            else if (avoidLeft || avoidRight)
            {
                // Handle horizontal violations without vertical violations
                if (avoidLeft)
                {
                    // Add all RIGHT positions first
                    resultList.Add(ContextMenuOpenDirection.TOP_RIGHT);
                    resultList.Add(ContextMenuOpenDirection.CENTER_RIGHT);
                    resultList.Add(ContextMenuOpenDirection.BOTTOM_RIGHT);
                }
                else if (avoidRight)
                {
                    // Add all LEFT positions first
                    resultList.Add(ContextMenuOpenDirection.TOP_LEFT);
                    resultList.Add(ContextMenuOpenDirection.CENTER_LEFT);
                    resultList.Add(ContextMenuOpenDirection.BOTTOM_LEFT);
                }
            }
            else
            {
                // No boundary violations, use standard fallback orders
                if (initialHorizontal == HorizontalPosition.LEFT)
                {
                    // Add LEFT positions first in appropriate vertical order
                    if (initialVertical != VerticalPosition.TOP && !resultList.Contains(ContextMenuOpenDirection.TOP_LEFT))
                        resultList.Add(ContextMenuOpenDirection.TOP_LEFT);
                        
                    if (initialVertical != VerticalPosition.CENTER && !resultList.Contains(ContextMenuOpenDirection.CENTER_LEFT))
                        resultList.Add(ContextMenuOpenDirection.CENTER_LEFT);
                        
                    if (initialVertical != VerticalPosition.BOTTOM && !resultList.Contains(ContextMenuOpenDirection.BOTTOM_LEFT))
                        resultList.Add(ContextMenuOpenDirection.BOTTOM_LEFT);
                    
                    // Then add RIGHT positions in same vertical order
                    resultList.Add(ContextMenuOpenDirection.TOP_RIGHT);
                    resultList.Add(ContextMenuOpenDirection.CENTER_RIGHT);
                    resultList.Add(ContextMenuOpenDirection.BOTTOM_RIGHT);
                }
                else // RIGHT
                {
                    // Add RIGHT positions first in appropriate vertical order
                    if (initialVertical != VerticalPosition.TOP && !resultList.Contains(ContextMenuOpenDirection.TOP_RIGHT))
                        resultList.Add(ContextMenuOpenDirection.TOP_RIGHT);
                        
                    if (initialVertical != VerticalPosition.CENTER && !resultList.Contains(ContextMenuOpenDirection.CENTER_RIGHT))
                        resultList.Add(ContextMenuOpenDirection.CENTER_RIGHT);
                        
                    if (initialVertical != VerticalPosition.BOTTOM && !resultList.Contains(ContextMenuOpenDirection.BOTTOM_RIGHT))
                        resultList.Add(ContextMenuOpenDirection.BOTTOM_RIGHT);
                    
                    // Then add LEFT positions in same vertical order
                    resultList.Add(ContextMenuOpenDirection.TOP_LEFT);
                    resultList.Add(ContextMenuOpenDirection.CENTER_LEFT);
                    resultList.Add(ContextMenuOpenDirection.BOTTOM_LEFT);
                }
            }
            
            // Remove duplicates - convert to array
            var result = resultList.ToArray();
            
            Debug.Log($"[ContextMenu] Fallback directions: {string.Join(", ", result)}");
            return result;
        }
        
        // Enum to represent horizontal position
        private enum HorizontalPosition
        {
            LEFT,
            RIGHT
        }
        
        // Enum to represent vertical position
        private enum VerticalPosition
        {
            TOP,
            CENTER,
            BOTTOM
        }
        
        // Helper method to extract horizontal position from direction
        private HorizontalPosition GetHorizontalPosition(ContextMenuOpenDirection direction)
        {
            switch (direction)
            {
                case ContextMenuOpenDirection.TOP_LEFT:
                case ContextMenuOpenDirection.CENTER_LEFT:
                case ContextMenuOpenDirection.BOTTOM_LEFT:
                    return HorizontalPosition.LEFT;
                    
                case ContextMenuOpenDirection.TOP_RIGHT:
                case ContextMenuOpenDirection.CENTER_RIGHT:
                case ContextMenuOpenDirection.BOTTOM_RIGHT:
                    return HorizontalPosition.RIGHT;
                    
                default:
                    return HorizontalPosition.LEFT; // Default to LEFT if unknown
            }
        }
        
        // Helper method to extract vertical position from direction
        private VerticalPosition GetVerticalPosition(ContextMenuOpenDirection direction)
        {
            switch (direction)
            {
                case ContextMenuOpenDirection.TOP_LEFT:
                case ContextMenuOpenDirection.TOP_RIGHT:
                    return VerticalPosition.TOP;
                
                case ContextMenuOpenDirection.CENTER_LEFT:
                case ContextMenuOpenDirection.CENTER_RIGHT:
                    return VerticalPosition.CENTER;
                
                case ContextMenuOpenDirection.BOTTOM_LEFT:
                case ContextMenuOpenDirection.BOTTOM_RIGHT:
                    return VerticalPosition.BOTTOM;
                
                default:
                    return VerticalPosition.CENTER; // Default to CENTER if unknown
            }
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

        private ContextMenuOpenDirection GetOppositeHorizontalDirection(ContextMenuOpenDirection direction)
        {
            return direction switch
            {
                ContextMenuOpenDirection.TOP_LEFT => ContextMenuOpenDirection.TOP_RIGHT,
                ContextMenuOpenDirection.CENTER_LEFT => ContextMenuOpenDirection.CENTER_RIGHT,
                ContextMenuOpenDirection.BOTTOM_LEFT => ContextMenuOpenDirection.BOTTOM_RIGHT,
                ContextMenuOpenDirection.TOP_RIGHT => ContextMenuOpenDirection.TOP_LEFT,
                ContextMenuOpenDirection.CENTER_RIGHT => ContextMenuOpenDirection.CENTER_LEFT,
                ContextMenuOpenDirection.BOTTOM_RIGHT => ContextMenuOpenDirection.BOTTOM_LEFT,
                _ => direction
            };
        }
        
        private ContextMenuOpenDirection GetOppositeVerticalDirection(ContextMenuOpenDirection direction)
        {
            return direction switch
            {
                ContextMenuOpenDirection.TOP_LEFT => ContextMenuOpenDirection.BOTTOM_LEFT,
                ContextMenuOpenDirection.TOP_RIGHT => ContextMenuOpenDirection.BOTTOM_RIGHT,
                ContextMenuOpenDirection.BOTTOM_LEFT => ContextMenuOpenDirection.TOP_LEFT,
                ContextMenuOpenDirection.BOTTOM_RIGHT => ContextMenuOpenDirection.TOP_RIGHT,
                _ => direction // Keep CENTER positions as is
            };
        }

        // Helper method to calculate percentage of menu that's out of bounds
        private float CalculateOutOfBoundsPercent(Rect container, Rect rect)
        {
            float menuArea = rect.width * rect.height;
            if (menuArea <= 0) return 0;
            
            float outOfBoundsArea = CalculateOutOfBoundsArea(container, rect);
            return outOfBoundsArea / menuArea;
        }
    }
}


