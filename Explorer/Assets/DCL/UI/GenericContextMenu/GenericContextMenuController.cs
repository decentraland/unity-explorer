using Cysharp.Threading.Tasks;
using DCL.UI.GenericContextMenu.Controls;
using DCL.UI.GenericContextMenu.Controls.Configs;
using MVC;
using System;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
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

    public class GenericContextMenuController : ControllerBase<GenericContextMenuView, GenericContextMenuParameter>, IDisposable
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private readonly ControlsPoolManager controlsPoolManager;
        private NativeArray<float3> worldRectCorners;
        private readonly ContextMenuOpenDirection[] openDirections = EnumUtils.Values<ContextMenuOpenDirection>();

        private NativeArray<ContextMenuOpenDirection> fallbackDirectionsCache;
        private int fallbackDirectionsCount;
        private NativeArray<float3> tempPositionCache;

        private RectTransform viewRectTransform;
        private float4 backgroundWorldRect;
        private UniTaskCompletionSource internalCloseTask;
        private bool isNativeArrayInitialized;

        public GenericContextMenuController(ViewFactoryMethod viewFactory,
            ControlsPoolManager controlsPoolManager) : base(viewFactory)
        {
            this.controlsPoolManager = controlsPoolManager;
            InitializeNativeArrays();
        }

        private void InitializeNativeArrays()
        {
            if (isNativeArrayInitialized) return;

            worldRectCorners = new NativeArray<float3>(4, Allocator.Persistent);
            fallbackDirectionsCache = new NativeArray<ContextMenuOpenDirection>(6, Allocator.Persistent);
            tempPositionCache = new NativeArray<float3>(2, Allocator.Persistent);
            isNativeArrayInitialized = true;
        }

        public override void Dispose()
        {
            base.Dispose();

            controlsPoolManager.Dispose();
            DisposeNativeArrays();
        }

        private void DisposeNativeArrays()
        {
            if (!isNativeArrayInitialized) return;

            if (worldRectCorners.IsCreated) worldRectCorners.Dispose();
            if (fallbackDirectionsCache.IsCreated) fallbackDirectionsCache.Dispose();
            if (tempPositionCache.IsCreated) tempPositionCache.Dispose();
            isNativeArrayInitialized = false;
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

        [BurstCompile]
        private static Vector2 GetOffsetByDirection(ContextMenuOpenDirection direction, Vector2 offsetFromTarget)
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

            tempPositionCache[0] = GetPositionForDirection(initialDirection, position);
            Vector2 offsetByDirection = GetOffsetByDirection(initialDirection, offsetFromTarget);
            var tempPos = tempPositionCache[0];
            tempPos.x += offsetByDirection.x;
            tempPos.y += offsetByDirection.y;
            tempPositionCache[0] = tempPos;

            Vector3 adjustedInitialPosition = ApplyContainerAdjustments(tempPositionCache[0], initialDirection);

            float4 boundaryRect = overlapRect.HasValue ?
                BurstRectUtils.RectToFloat4(overlapRect.Value) :
                backgroundWorldRect;

            float menuWidth = viewInstance!.ControlsContainer.rect.width;
            float menuHeight = viewInstance!.ControlsContainer.rect.height;

            float4 menuRect = GetProjectedRect(adjustedInitialPosition);

            float outOfBoundsPercentTop = 0;
            float outOfBoundsPercentBottom = 0;
            float outOfBoundsPercentRight = 0;
            float outOfBoundsPercentLeft = 0;

            BurstRectUtils.CalculateOutOfBoundsPercentages(
                ref outOfBoundsPercentTop,
                ref outOfBoundsPercentBottom,
                ref outOfBoundsPercentRight,
                ref outOfBoundsPercentLeft,
                menuRect, boundaryRect, menuWidth, menuHeight);

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

            ContextMenuOpenDirection smartDirection = GetSmartDirection(
                initialDirection,
                initialHorizontal,
                initialVertical,
                outOfBoundsOnRight,
                outOfBoundsOnLeft,
                outOfBoundsOnTop,
                outOfBoundsOnBottom);

            tempPositionCache[1] = GetPositionForDirection(smartDirection, position);
            Vector2 offsetBySmartDirection = GetOffsetByDirection(smartDirection, offsetFromTarget);
            var tempSmartPos = tempPositionCache[1];
            tempSmartPos.x += offsetBySmartDirection.x;
            tempSmartPos.y += offsetBySmartDirection.y;
            tempPositionCache[1] = tempSmartPos;

            Vector3 adjustedBasePosition = ApplyContainerAdjustments(tempPositionCache[1], smartDirection);

            float4 smartMenuRect = GetProjectedRect(adjustedBasePosition);

            bool isWithinBounds = BurstRectUtils.IsRectContained(boundaryRect, smartMenuRect);

            if (isWithinBounds)
            {
                return adjustedBasePosition;
            }

            float smartOutOfBoundsPercent = BurstRectUtils.CalculateOutOfBoundsPercent(boundaryRect, smartMenuRect);

            Vector3 adjustedPosition = AdjustPositionToFitBounds(adjustedBasePosition, boundaryRect);
            float4 adjustedMenuRect = GetProjectedRect(adjustedPosition);
            bool adjustedIsWithinBounds = BurstRectUtils.IsRectContained(boundaryRect, adjustedMenuRect);
            float adjustedOutOfBoundsPercent = adjustedIsWithinBounds ? 0 : BurstRectUtils.CalculateOutOfBoundsPercent(boundaryRect, adjustedMenuRect);

            if (adjustedIsWithinBounds)
            {
                return adjustedPosition;
            }

            GetFallbackDirections(smartDirection);

            float bestOutOfBoundsPercent = adjustedOutOfBoundsPercent;
            Vector3 bestPosition = adjustedPosition;
            bool foundPerfectPosition = false;

            for (int i = 0; i < fallbackDirectionsCount; i++)
            {
                ContextMenuOpenDirection currentDirection = fallbackDirectionsCache[i];

                if (currentDirection == smartDirection)
                    continue;

                Vector3 currentAnchoredPosition = GetPositionForDirection(currentDirection, position);

                for (int j = 0; j < openDirections.Length; j++)
                {
                    ContextMenuOpenDirection offsetDirection = openDirections[j];
                    Vector2 currentOffsetByDirection = GetOffsetByDirection(offsetDirection, offsetFromTarget);

                    var tempPosForLoop = tempPositionCache[0];
                    tempPosForLoop.x = currentAnchoredPosition.x + currentOffsetByDirection.x;
                    tempPosForLoop.y = currentAnchoredPosition.y + currentOffsetByDirection.y;
                    tempPosForLoop.z = currentAnchoredPosition.z;
                    tempPositionCache[0] = tempPosForLoop;

                    Vector3 adjustedCurrentPosition = ApplyContainerAdjustments(tempPositionCache[0], offsetDirection);

                    Vector3 boundaryAdjustedPosition = AdjustPositionToFitBounds(adjustedCurrentPosition, boundaryRect);
                    float4 currentMenuRect = GetProjectedRect(boundaryAdjustedPosition);
                    bool currentIsWithinBounds = BurstRectUtils.IsRectContained(boundaryRect, currentMenuRect);

                    if (currentIsWithinBounds)
                    {
                        return boundaryAdjustedPosition;
                    }

                    float currentOutOfBoundsPercent = BurstRectUtils.CalculateOutOfBoundsPercent(boundaryRect, currentMenuRect);
                    if (currentOutOfBoundsPercent < bestOutOfBoundsPercent)
                    {
                        bestPosition = boundaryAdjustedPosition;
                        bestOutOfBoundsPercent = currentOutOfBoundsPercent;
                    }
                }
            }

            return bestPosition;
        }

        [BurstCompile]
        private static ContextMenuOpenDirection GetSmartDirection(
            ContextMenuOpenDirection initialDirection,
            HorizontalPosition initialHorizontal,
            VerticalPosition initialVertical,
            bool outOfBoundsOnRight,
            bool outOfBoundsOnLeft,
            bool outOfBoundsOnTop,
            bool outOfBoundsOnBottom)
        {
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

            return smartDirection;
        }

        [BurstCompile]
        private Vector3 AdjustPositionToFitBounds(Vector3 position, float4 boundaryRect)
        {
            Vector3 adjustedPosition = position;
            float4 menuRect = GetProjectedRect(position);

            if (menuRect.x < boundaryRect.x)
            {
                float adjustment = boundaryRect.x - menuRect.x;
                adjustedPosition.x += adjustment;
            }
            else if (menuRect.x + menuRect.z > boundaryRect.x + boundaryRect.z)
            {
                float adjustment = (menuRect.x + menuRect.z) - (boundaryRect.x + boundaryRect.z);
                adjustedPosition.x -= adjustment;
            }

            if (menuRect.y < boundaryRect.y)
            {
                float adjustment = boundaryRect.y - menuRect.y;
                adjustedPosition.y += adjustment;
            }
            else if (menuRect.y + menuRect.w > boundaryRect.y + boundaryRect.w)
            {
                float adjustment = (menuRect.y + menuRect.w) - (boundaryRect.y + boundaryRect.w);
                adjustedPosition.y -= adjustment;
            }

            return adjustedPosition;
        }

        private void GetFallbackDirections(ContextMenuOpenDirection initialDirection)
        {
            fallbackDirectionsCount = 0;

            HorizontalPosition initialHorizontal = GetHorizontalPosition(initialDirection);
            VerticalPosition initialVertical = GetVerticalPosition(initialDirection);

            float outOfBoundsPercentTop = 0;
            float outOfBoundsPercentBottom = 0;
            float outOfBoundsPercentRight = 0;
            float outOfBoundsPercentLeft = 0;

            float menuHeight = viewInstance!.ControlsContainer.rect.height;
            float menuWidth = viewInstance!.ControlsContainer.rect.width;

            Vector2 anchorPosition = inputData.AnchorPosition;

            float4 boundaryRect = inputData.OverlapRect.HasValue ?
                BurstRectUtils.RectToFloat4(inputData.OverlapRect.Value) :
                backgroundWorldRect;

            Vector3 menuPosition = GetPositionForDirection(initialDirection, viewRectTransform.InverseTransformPoint(anchorPosition));
            menuPosition = ApplyContainerAdjustments(menuPosition, initialDirection);
            float4 menuRect = GetProjectedRect(menuPosition);

            BurstRectUtils.CalculateOutOfBoundsPercentages(
                ref outOfBoundsPercentTop,
                ref outOfBoundsPercentBottom,
                ref outOfBoundsPercentRight,
                ref outOfBoundsPercentLeft,
                menuRect, boundaryRect, menuWidth, menuHeight);

            const float SEVERE_BOUNDARY_VIOLATION_THRESHOLD = 0.4f;

            bool avoidTop = outOfBoundsPercentTop > 0;
            bool avoidBottom = outOfBoundsPercentBottom > 0;
            bool skipCenter = outOfBoundsPercentTop > SEVERE_BOUNDARY_VIOLATION_THRESHOLD ||
                               outOfBoundsPercentBottom > SEVERE_BOUNDARY_VIOLATION_THRESHOLD;

            bool avoidRight = outOfBoundsPercentRight > 0;
            bool avoidLeft = outOfBoundsPercentLeft > 0;

            if ((initialVertical == VerticalPosition.TOP && !avoidTop) ||
                (initialVertical == VerticalPosition.BOTTOM && !avoidBottom) ||
                (initialVertical == VerticalPosition.CENTER) ||
                (initialHorizontal == HorizontalPosition.LEFT && !avoidLeft) ||
                (initialHorizontal == HorizontalPosition.RIGHT && !avoidRight))
            {
                AddToFallbackDirections(initialDirection);
            }

            ProcessTopBoundaryViolation(
                outOfBoundsPercentTop,
                outOfBoundsPercentBottom,
                initialHorizontal,
                skipCenter,
                avoidBottom,
                avoidRight,
                avoidLeft);

            ProcessBottomBoundaryViolation(
                outOfBoundsPercentBottom,
                initialHorizontal,
                skipCenter,
                avoidTop,
                avoidRight,
                avoidLeft);

            ProcessHorizontalBoundaryViolation(
                avoidLeft,
                avoidRight);

            ProcessNoBoundaryViolation(
                initialHorizontal,
                initialVertical,
                outOfBoundsPercentTop,
                outOfBoundsPercentBottom);
        }

        private void ProcessTopBoundaryViolation(
            float outOfBoundsPercentTop,
            float outOfBoundsPercentBottom,
            HorizontalPosition initialHorizontal,
            bool skipCenter,
            bool avoidBottom,
            bool avoidRight,
            bool avoidLeft)
        {
            if (outOfBoundsPercentTop <= outOfBoundsPercentBottom || outOfBoundsPercentTop <= 0) return;

            if (initialHorizontal == HorizontalPosition.LEFT)
            {
                AddToFallbackDirections(ContextMenuOpenDirection.BOTTOM_LEFT);
                if (!skipCenter && !avoidBottom)
                    AddToFallbackDirections(ContextMenuOpenDirection.CENTER_LEFT);

                if (!avoidRight)
                {
                    AddToFallbackDirections(ContextMenuOpenDirection.BOTTOM_RIGHT);
                    if (!skipCenter && !avoidBottom)
                        AddToFallbackDirections(ContextMenuOpenDirection.CENTER_RIGHT);
                }
            }
            else
            {
                AddToFallbackDirections(ContextMenuOpenDirection.BOTTOM_RIGHT);
                if (!skipCenter && !avoidBottom)
                    AddToFallbackDirections(ContextMenuOpenDirection.CENTER_RIGHT);

                if (!avoidLeft)
                {
                    AddToFallbackDirections(ContextMenuOpenDirection.BOTTOM_LEFT);
                    if (!skipCenter && !avoidBottom)
                        AddToFallbackDirections(ContextMenuOpenDirection.CENTER_LEFT);
                }
            }

            if (!ContainsDirection(ContextMenuOpenDirection.TOP_LEFT) && !avoidLeft)
                AddToFallbackDirections(ContextMenuOpenDirection.TOP_LEFT);
            if (!ContainsDirection(ContextMenuOpenDirection.TOP_RIGHT) && !avoidRight)
                AddToFallbackDirections(ContextMenuOpenDirection.TOP_RIGHT);
        }

        private void ProcessBottomBoundaryViolation(
            float outOfBoundsPercentBottom,
            HorizontalPosition initialHorizontal,
            bool skipCenter,
            bool avoidTop,
            bool avoidRight,
            bool avoidLeft)
        {
            if (outOfBoundsPercentBottom <= 0) return;

            if (initialHorizontal == HorizontalPosition.LEFT)
            {
                AddToFallbackDirections(ContextMenuOpenDirection.TOP_LEFT);
                if (!skipCenter && !avoidTop)
                    AddToFallbackDirections(ContextMenuOpenDirection.CENTER_LEFT);

                if (!avoidRight)
                {
                    AddToFallbackDirections(ContextMenuOpenDirection.TOP_RIGHT);
                    if (!skipCenter && !avoidTop)
                        AddToFallbackDirections(ContextMenuOpenDirection.CENTER_RIGHT);
                }
            }
            else
            {
                AddToFallbackDirections(ContextMenuOpenDirection.TOP_RIGHT);
                if (!skipCenter && !avoidTop)
                    AddToFallbackDirections(ContextMenuOpenDirection.CENTER_RIGHT);

                if (!avoidLeft)
                {
                    AddToFallbackDirections(ContextMenuOpenDirection.TOP_LEFT);
                    if (!skipCenter && !avoidTop)
                        AddToFallbackDirections(ContextMenuOpenDirection.CENTER_LEFT);
                }
            }

            if (!ContainsDirection(ContextMenuOpenDirection.BOTTOM_LEFT) && !avoidLeft)
                AddToFallbackDirections(ContextMenuOpenDirection.BOTTOM_LEFT);
            if (!ContainsDirection(ContextMenuOpenDirection.BOTTOM_RIGHT) && !avoidRight)
                AddToFallbackDirections(ContextMenuOpenDirection.BOTTOM_RIGHT);
        }

        private void ProcessHorizontalBoundaryViolation(bool avoidLeft, bool avoidRight)
        {
            if (!avoidLeft && !avoidRight) return;

            if (avoidLeft)
            {
                AddToFallbackDirections(ContextMenuOpenDirection.TOP_RIGHT);
                AddToFallbackDirections(ContextMenuOpenDirection.CENTER_RIGHT);
                AddToFallbackDirections(ContextMenuOpenDirection.BOTTOM_RIGHT);
            }
            else if (avoidRight)
            {
                AddToFallbackDirections(ContextMenuOpenDirection.TOP_LEFT);
                AddToFallbackDirections(ContextMenuOpenDirection.CENTER_LEFT);
                AddToFallbackDirections(ContextMenuOpenDirection.BOTTOM_LEFT);
            }
        }

        private void ProcessNoBoundaryViolation(
            HorizontalPosition initialHorizontal,
            VerticalPosition initialVertical,
            float outOfBoundsPercentTop,
            float outOfBoundsPercentBottom)
        {
            if (outOfBoundsPercentTop > 0 || outOfBoundsPercentBottom > 0) return;

            if (initialHorizontal == HorizontalPosition.LEFT)
            {
                if (initialVertical != VerticalPosition.TOP && !ContainsDirection(ContextMenuOpenDirection.TOP_LEFT))
                    AddToFallbackDirections(ContextMenuOpenDirection.TOP_LEFT);

                if (initialVertical != VerticalPosition.CENTER && !ContainsDirection(ContextMenuOpenDirection.CENTER_LEFT))
                    AddToFallbackDirections(ContextMenuOpenDirection.CENTER_LEFT);

                if (initialVertical != VerticalPosition.BOTTOM && !ContainsDirection(ContextMenuOpenDirection.BOTTOM_LEFT))
                    AddToFallbackDirections(ContextMenuOpenDirection.BOTTOM_LEFT);

                AddToFallbackDirections(ContextMenuOpenDirection.TOP_RIGHT);
                AddToFallbackDirections(ContextMenuOpenDirection.CENTER_RIGHT);
                AddToFallbackDirections(ContextMenuOpenDirection.BOTTOM_RIGHT);
            }
            else
            {
                if (initialVertical != VerticalPosition.TOP && !ContainsDirection(ContextMenuOpenDirection.TOP_RIGHT))
                    AddToFallbackDirections(ContextMenuOpenDirection.TOP_RIGHT);

                if (initialVertical != VerticalPosition.CENTER && !ContainsDirection(ContextMenuOpenDirection.CENTER_RIGHT))
                    AddToFallbackDirections(ContextMenuOpenDirection.CENTER_RIGHT);

                if (initialVertical != VerticalPosition.BOTTOM && !ContainsDirection(ContextMenuOpenDirection.BOTTOM_RIGHT))
                    AddToFallbackDirections(ContextMenuOpenDirection.BOTTOM_RIGHT);

                AddToFallbackDirections(ContextMenuOpenDirection.TOP_LEFT);
                AddToFallbackDirections(ContextMenuOpenDirection.CENTER_LEFT);
                AddToFallbackDirections(ContextMenuOpenDirection.BOTTOM_LEFT);
            }
        }

        private void AddToFallbackDirections(ContextMenuOpenDirection direction)
        {
            if (fallbackDirectionsCount < fallbackDirectionsCache.Length)
            {
                fallbackDirectionsCache[fallbackDirectionsCount++] = direction;
            }
        }

        [BurstCompile]
        private bool ContainsDirection(ContextMenuOpenDirection direction)
        {
            for (int i = 0; i < fallbackDirectionsCount; i++)
            {
                if (fallbackDirectionsCache[i] == direction)
                    return true;
            }
            return false;
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

        [BurstCompile]
        private static HorizontalPosition GetHorizontalPosition(ContextMenuOpenDirection direction)
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

        [BurstCompile]
        private static VerticalPosition GetVerticalPosition(ContextMenuOpenDirection direction)
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

        [BurstCompile]
        private float3 GetPositionForDirection(ContextMenuOpenDirection direction, Vector3 position)
        {
            float halfWidth = viewInstance!.ControlsContainer.rect.width / 2;
            float halfHeight = viewInstance!.ControlsContainer.rect.height / 2;
            float3 result = new float3(position.x, position.y, position.z);

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

        private float4 GetProjectedRect(Vector3 newPosition)
        {
            Vector3 originalPosition = viewInstance!.ControlsContainer.localPosition;
            viewInstance!.ControlsContainer.localPosition = newPosition;
            float4 rect = GetWorldRect(viewInstance!.ControlsContainer);
            viewInstance!.ControlsContainer.localPosition = originalPosition;

            return rect;
        }

        private float4 GetWorldRect(RectTransform rectTransform)
        {
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);

            for (var i = 0; i < 4; i++)
            {
                worldRectCorners[i] = new float3(corners[i].x, corners[i].y, corners[i].z);
            }

            float minX = corners[0].x;
            float minY = corners[0].y;
            float maxX = corners[2].x;
            float maxY = corners[2].y;

            return new float4(minX, minY, maxX - minX, maxY - minY);
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

        [BurstCompile]
        private static Vector3 ApplyContainerAdjustments(float3 position, ContextMenuOpenDirection direction) =>
            new (position.x, position.y, position.z);

        [BurstCompile]
        private static ContextMenuOpenDirection GetOppositeHorizontalDirection(ContextMenuOpenDirection direction)
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

        [BurstCompile]
        private static ContextMenuOpenDirection GetOppositeVerticalDirection(ContextMenuOpenDirection direction)
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
    }
}


