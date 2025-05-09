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

            Vector3 basePosition = GetPositionForDirection(initialDirection, position);
            Vector2 offsetByDirection = GetOffsetByDirection(initialDirection, offsetFromTarget);
            Vector3 initialPosition = basePosition + new Vector3(offsetByDirection.x, offsetByDirection.y, 0);
            Vector3 adjustedInitialPosition = ApplyContainerAdjustments(initialPosition, initialDirection);
            
            Rect boundaryRect = overlapRect ?? backgroundWorldRect;
            
            float menuWidth = viewInstance!.ControlsContainer.rect.width;
            float menuHeight = viewInstance!.ControlsContainer.rect.height;
            
            Rect menuRect = GetProjectedRect(adjustedInitialPosition);
            
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
            
            float totalOutOfBoundsPercent = outOfBoundsPercentTop + outOfBoundsPercentBottom + 
                                           outOfBoundsPercentRight + outOfBoundsPercentLeft;
            
            const float MINIMAL_ADJUSTMENT_THRESHOLD = 0.01f;
            
            if (totalOutOfBoundsPercent < MINIMAL_ADJUSTMENT_THRESHOLD)
            {
                return adjustedInitialPosition;
            }
            
            bool outOfBoundsOnRight = outOfBoundsPercentRight > 0;
            bool outOfBoundsOnLeft = outOfBoundsPercentLeft > 0;
            bool outOfBoundsOnTop = outOfBoundsPercentTop > 0;
            bool outOfBoundsOnBottom = outOfBoundsPercentBottom > 0;
            
            HorizontalPosition initialHorizontal = GetHorizontalPosition(initialDirection);
            VerticalPosition initialVertical = GetVerticalPosition(initialDirection);
            
            ContextMenuOpenDirection smartDirection = initialDirection;
            
            if (initialHorizontal == HorizontalPosition.RIGHT && outOfBoundsOnRight)
            {
                smartDirection = GetOppositeHorizontalDirection(initialDirection);
            }
            else if (initialHorizontal == HorizontalPosition.LEFT && outOfBoundsOnLeft)
            {
                smartDirection = GetOppositeHorizontalDirection(initialDirection);
            }
            
            if (initialVertical == VerticalPosition.TOP && outOfBoundsOnTop)
            {
                smartDirection = GetOppositeVerticalDirection(smartDirection);
            }
            else if (initialVertical == VerticalPosition.BOTTOM && outOfBoundsOnBottom)
            {
                smartDirection = GetOppositeVerticalDirection(smartDirection);
            }
            
            Vector3 anchoredPosition = GetPositionForDirection(smartDirection, position);
            Vector2 offsetBySmartDirection = GetOffsetByDirection(smartDirection, offsetFromTarget);
            Vector3 baseSmartPosition = anchoredPosition + new Vector3(offsetBySmartDirection.x, offsetBySmartDirection.y, 0);
            
            Vector3 adjustedBasePosition = ApplyContainerAdjustments(baseSmartPosition, smartDirection);
            
            Rect smartMenuRect = GetProjectedRect(adjustedBasePosition);
            
            bool isWithinBounds = IsRectContained(boundaryRect, smartMenuRect);
            
            if (isWithinBounds)
            {
                return adjustedBasePosition;
            }
            
            float smartOutOfBoundsPercent = CalculateOutOfBoundsPercent(boundaryRect, smartMenuRect);
            
            Vector3 adjustedPosition = AdjustPositionToFitBounds(adjustedBasePosition, boundaryRect);
            Rect adjustedMenuRect = GetProjectedRect(adjustedPosition);
            bool adjustedIsWithinBounds = IsRectContained(boundaryRect, adjustedMenuRect);
            float adjustedOutOfBoundsPercent = adjustedIsWithinBounds ? 0 : CalculateOutOfBoundsPercent(boundaryRect, adjustedMenuRect);
            
            if (adjustedIsWithinBounds)
            {
                return adjustedPosition;
            }
            
            ContextMenuOpenDirection[] fallbackOrder = GetFallbackDirections(smartDirection);

            float bestOutOfBoundsPercent = adjustedOutOfBoundsPercent;
            Vector3 bestPosition = adjustedPosition;
            bool foundPerfectPosition = false;

            foreach (var currentDirection in fallbackOrder)
            {
                if (currentDirection == smartDirection)
                    continue;
                    
                Vector3 currentAnchoredPosition = GetPositionForDirection(currentDirection, position);

                foreach (ContextMenuOpenDirection offsetDirection in openDirections)
                {
                    Vector2 currentOffsetByDirection = GetOffsetByDirection(offsetDirection, offsetFromTarget);
                    Vector3 currentPosition = currentAnchoredPosition + new Vector3(currentOffsetByDirection.x, currentOffsetByDirection.y, 0);
                    
                    Vector3 adjustedCurrentPosition = ApplyContainerAdjustments(currentPosition, offsetDirection);
                    
                    Vector3 boundaryAdjustedPosition = AdjustPositionToFitBounds(adjustedCurrentPosition, boundaryRect);
                    Rect currentMenuRect = GetProjectedRect(boundaryAdjustedPosition);
                    bool currentIsWithinBounds = IsRectContained(boundaryRect, currentMenuRect);
                    
                    if (currentIsWithinBounds)
                    {
                        return boundaryAdjustedPosition;
                    }
                    
                    float currentOutOfBoundsPercent = CalculateOutOfBoundsPercent(boundaryRect, currentMenuRect);
                    if (currentOutOfBoundsPercent < bestOutOfBoundsPercent)
                    {
                        bestPosition = boundaryAdjustedPosition;
                        bestOutOfBoundsPercent = currentOutOfBoundsPercent;
                    }
                }
            }

            return bestPosition;
        }

        private bool IsRectContained(Rect container, Rect rect)
        {
            return rect.xMin >= container.xMin && 
                   rect.xMax <= container.xMax && 
                   rect.yMin >= container.yMin && 
                   rect.yMax <= container.yMax;
        }
        
        private float CalculateOutOfBoundsArea(Rect container, Rect rect)
        {
            float outOfBoundsWidth = 0;
            float outOfBoundsHeight = 0;
            
            if (rect.xMin < container.xMin)
                outOfBoundsWidth += container.xMin - rect.xMin;
            if (rect.xMax > container.xMax)
                outOfBoundsWidth += rect.xMax - container.xMax;
                
            if (rect.yMin < container.yMin)
                outOfBoundsHeight += container.yMin - rect.yMin;
            if (rect.yMax > container.yMax)
                outOfBoundsHeight += rect.yMax - container.yMax;
                
            return outOfBoundsWidth * rect.height + outOfBoundsHeight * rect.width - (outOfBoundsWidth * outOfBoundsHeight);
        }
        
        private Vector3 AdjustPositionToFitBounds(Vector3 position, Rect boundaryRect)
        {
            Vector3 adjustedPosition = position;
            Rect menuRect = GetProjectedRect(position);
            
            if (menuRect.xMin < boundaryRect.xMin)
            {
                float adjustment = boundaryRect.xMin - menuRect.xMin;
                adjustedPosition.x += adjustment;
            }
            else if (menuRect.xMax > boundaryRect.xMax)
            {
                float adjustment = menuRect.xMax - boundaryRect.xMax;
                adjustedPosition.x -= adjustment;
            }
            
            if (menuRect.yMin < boundaryRect.yMin)
            {
                float adjustment = boundaryRect.yMin - menuRect.yMin;
                adjustedPosition.y += adjustment;
            }
            else if (menuRect.yMax > boundaryRect.yMax)
            {
                float adjustment = menuRect.yMax - boundaryRect.yMax;
                adjustedPosition.y -= adjustment;
            }
            
            return adjustedPosition;
        }

        private ContextMenuOpenDirection[] GetFallbackDirections(ContextMenuOpenDirection initialDirection)
        {
            HorizontalPosition initialHorizontal = GetHorizontalPosition(initialDirection);
            VerticalPosition initialVertical = GetVerticalPosition(initialDirection);
            
            float outOfBoundsPercentTop = 0;
            float outOfBoundsPercentBottom = 0;
            float outOfBoundsPercentRight = 0;
            float outOfBoundsPercentLeft = 0;
            
            float menuHeight = viewInstance!.ControlsContainer.rect.height;
            float menuWidth = viewInstance!.ControlsContainer.rect.width;
            
            Vector2 anchorPosition = inputData.AnchorPosition;
            
            Rect boundaryRect = inputData.OverlapRect ?? backgroundWorldRect;
            
            Vector3 menuPosition = GetPositionForDirection(initialDirection, viewRectTransform.InverseTransformPoint(anchorPosition));
            menuPosition = ApplyContainerAdjustments(menuPosition, initialDirection);
            Rect menuRect = GetProjectedRect(menuPosition);
            
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
            
            const float SEVERE_BOUNDARY_VIOLATION_THRESHOLD = 0.4f;
            
            bool avoidTop = outOfBoundsPercentTop > 0;
            bool avoidBottom = outOfBoundsPercentBottom > 0;
            bool skipCenter = outOfBoundsPercentTop > SEVERE_BOUNDARY_VIOLATION_THRESHOLD || 
                               outOfBoundsPercentBottom > SEVERE_BOUNDARY_VIOLATION_THRESHOLD;
            
            bool avoidRight = outOfBoundsPercentRight > 0;
            bool avoidLeft = outOfBoundsPercentLeft > 0;
            
            var resultList = new System.Collections.Generic.List<ContextMenuOpenDirection>(6);
            
            if ((initialVertical == VerticalPosition.TOP && !avoidTop) ||
                (initialVertical == VerticalPosition.BOTTOM && !avoidBottom) ||
                (initialVertical == VerticalPosition.CENTER) ||
                (initialHorizontal == HorizontalPosition.LEFT && !avoidLeft) ||
                (initialHorizontal == HorizontalPosition.RIGHT && !avoidRight))
            {
                resultList.Add(initialDirection);
            }
            
            if (outOfBoundsPercentTop > outOfBoundsPercentBottom && outOfBoundsPercentTop > 0)
            {
                if (initialHorizontal == HorizontalPosition.LEFT)
                {
                    resultList.Add(ContextMenuOpenDirection.BOTTOM_LEFT);
                    if (!skipCenter && !avoidBottom) resultList.Add(ContextMenuOpenDirection.CENTER_LEFT);
                    
                    if (!avoidRight)
                    {
                        resultList.Add(ContextMenuOpenDirection.BOTTOM_RIGHT);
                        if (!skipCenter && !avoidBottom) resultList.Add(ContextMenuOpenDirection.CENTER_RIGHT);
                    }
                }
                else
                {
                    resultList.Add(ContextMenuOpenDirection.BOTTOM_RIGHT);
                    if (!skipCenter && !avoidBottom) resultList.Add(ContextMenuOpenDirection.CENTER_RIGHT);
                    
                    if (!avoidLeft)
                    {
                        resultList.Add(ContextMenuOpenDirection.BOTTOM_LEFT);
                        if (!skipCenter && !avoidBottom) resultList.Add(ContextMenuOpenDirection.CENTER_LEFT);
                    }
                }
                
                if (!resultList.Contains(ContextMenuOpenDirection.TOP_LEFT) && !avoidLeft) 
                    resultList.Add(ContextMenuOpenDirection.TOP_LEFT);
                if (!resultList.Contains(ContextMenuOpenDirection.TOP_RIGHT) && !avoidRight) 
                    resultList.Add(ContextMenuOpenDirection.TOP_RIGHT);
            }
            else if (outOfBoundsPercentBottom > 0)
            {
                if (initialHorizontal == HorizontalPosition.LEFT)
                {
                    resultList.Add(ContextMenuOpenDirection.TOP_LEFT);
                    if (!skipCenter && !avoidTop) resultList.Add(ContextMenuOpenDirection.CENTER_LEFT);
                    
                    if (!avoidRight)
                    {
                        resultList.Add(ContextMenuOpenDirection.TOP_RIGHT);
                        if (!skipCenter && !avoidTop) resultList.Add(ContextMenuOpenDirection.CENTER_RIGHT);
                    }
                }
                else
                {
                    resultList.Add(ContextMenuOpenDirection.TOP_RIGHT);
                    if (!skipCenter && !avoidTop) resultList.Add(ContextMenuOpenDirection.CENTER_RIGHT);
                    
                    if (!avoidLeft)
                    {
                        resultList.Add(ContextMenuOpenDirection.TOP_LEFT);
                        if (!skipCenter && !avoidTop) resultList.Add(ContextMenuOpenDirection.CENTER_LEFT);
                    }
                }
                
                if (!resultList.Contains(ContextMenuOpenDirection.BOTTOM_LEFT) && !avoidLeft) 
                    resultList.Add(ContextMenuOpenDirection.BOTTOM_LEFT);
                if (!resultList.Contains(ContextMenuOpenDirection.BOTTOM_RIGHT) && !avoidRight) 
                    resultList.Add(ContextMenuOpenDirection.BOTTOM_RIGHT);
            }
            else if (avoidLeft || avoidRight)
            {
                if (avoidLeft)
                {
                    resultList.Add(ContextMenuOpenDirection.TOP_RIGHT);
                    resultList.Add(ContextMenuOpenDirection.CENTER_RIGHT);
                    resultList.Add(ContextMenuOpenDirection.BOTTOM_RIGHT);
                }
                else if (avoidRight)
                {
                    resultList.Add(ContextMenuOpenDirection.TOP_LEFT);
                    resultList.Add(ContextMenuOpenDirection.CENTER_LEFT);
                    resultList.Add(ContextMenuOpenDirection.BOTTOM_LEFT);
                }
            }
            else
            {
                if (initialHorizontal == HorizontalPosition.LEFT)
                {
                    if (initialVertical != VerticalPosition.TOP && !resultList.Contains(ContextMenuOpenDirection.TOP_LEFT))
                        resultList.Add(ContextMenuOpenDirection.TOP_LEFT);
                        
                    if (initialVertical != VerticalPosition.CENTER && !resultList.Contains(ContextMenuOpenDirection.CENTER_LEFT))
                        resultList.Add(ContextMenuOpenDirection.CENTER_LEFT);
                        
                    if (initialVertical != VerticalPosition.BOTTOM && !resultList.Contains(ContextMenuOpenDirection.BOTTOM_LEFT))
                        resultList.Add(ContextMenuOpenDirection.BOTTOM_LEFT);
                    
                    resultList.Add(ContextMenuOpenDirection.TOP_RIGHT);
                    resultList.Add(ContextMenuOpenDirection.CENTER_RIGHT);
                    resultList.Add(ContextMenuOpenDirection.BOTTOM_RIGHT);
                }
                else
                {
                    if (initialVertical != VerticalPosition.TOP && !resultList.Contains(ContextMenuOpenDirection.TOP_RIGHT))
                        resultList.Add(ContextMenuOpenDirection.TOP_RIGHT);
                        
                    if (initialVertical != VerticalPosition.CENTER && !resultList.Contains(ContextMenuOpenDirection.CENTER_RIGHT))
                        resultList.Add(ContextMenuOpenDirection.CENTER_RIGHT);
                        
                    if (initialVertical != VerticalPosition.BOTTOM && !resultList.Contains(ContextMenuOpenDirection.BOTTOM_RIGHT))
                        resultList.Add(ContextMenuOpenDirection.BOTTOM_RIGHT);
                    
                    resultList.Add(ContextMenuOpenDirection.TOP_LEFT);
                    resultList.Add(ContextMenuOpenDirection.CENTER_LEFT);
                    resultList.Add(ContextMenuOpenDirection.BOTTOM_LEFT);
                }
            }
            
            var result = resultList.ToArray();
            
            return result;
        }
        
        private enum HorizontalPosition
        {
            LEFT,
            RIGHT
        }
        
        private enum VerticalPosition
        {
            TOP,
            CENTER,
            BOTTOM
        }
        
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
                    return HorizontalPosition.LEFT;
            }
        }
        
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
                    return VerticalPosition.CENTER;
            }
        }

        private Vector3 GetPositionForDirection(ContextMenuOpenDirection direction, Vector3 position)
        {
            float halfWidth = viewInstance!.ControlsContainer.rect.width / 2;
            float halfHeight = viewInstance!.ControlsContainer.rect.height / 2;
            Vector3 result = position;
            
            switch (direction)
            {
                case ContextMenuOpenDirection.TOP_LEFT:
                case ContextMenuOpenDirection.BOTTOM_LEFT:
                case ContextMenuOpenDirection.CENTER_LEFT:
                    result.x -= halfWidth;
                    break;
                case ContextMenuOpenDirection.TOP_RIGHT:
                case ContextMenuOpenDirection.BOTTOM_RIGHT:
                case ContextMenuOpenDirection.CENTER_RIGHT:
                    result.x += halfWidth;
                    break;
            }
            
            switch (direction)
            {
                case ContextMenuOpenDirection.TOP_LEFT:
                case ContextMenuOpenDirection.TOP_RIGHT:
                    result.y += halfHeight;
                    break;
                case ContextMenuOpenDirection.BOTTOM_LEFT:
                case ContextMenuOpenDirection.BOTTOM_RIGHT:
                    result.y -= halfHeight;
                    break;
                case ContextMenuOpenDirection.CENTER_LEFT:
                case ContextMenuOpenDirection.CENTER_RIGHT:
                    break;
            }

            return result;
        }

        private float CalculateIntersectionArea(Rect rect1, Rect rect2)
        {
            Rect intersection = CalculateIntersection(rect1, rect2);
            
            if (intersection.width <= 0 || intersection.height <= 0)
                return 0;
                
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
                _ => direction
            };
        }
        
        private float CalculateOutOfBoundsPercent(Rect container, Rect rect)
        {
            float menuArea = rect.width * rect.height;
            if (menuArea <= 0) return 0;
            
            float outOfBoundsArea = CalculateOutOfBoundsArea(container, rect);
            return outOfBoundsArea / menuArea;
        }
    }
}


