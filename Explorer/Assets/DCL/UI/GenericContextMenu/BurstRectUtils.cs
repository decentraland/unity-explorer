using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.UI
{
    /// <summary>
    /// Utility class for rectangle operations that are compatible with Burst compilation.
    /// Uses float4 instead of Rect for better performance with Burst.
    /// </summary>
    [BurstCompile]
    public static class BurstRectUtils 
    {
        // float4 used as rect: x,y = position, z,w = width,height
        
        [BurstCompile]
        public static bool IsRectContained(float4 container, float4 rect)
        {
            return rect.x >= container.x && 
                   rect.x + rect.z <= container.x + container.z && 
                   rect.y >= container.y && 
                   rect.y + rect.w <= container.y + container.w;
        }
        
        [BurstCompile]
        public static float CalculateOutOfBoundsArea(float4 container, float4 rect)
        {
            float outOfBoundsWidth = 0;
            float outOfBoundsHeight = 0;
            
            if (rect.x < container.x)
                outOfBoundsWidth += container.x - rect.x;
            if (rect.x + rect.z > container.x + container.z)
                outOfBoundsWidth += (rect.x + rect.z) - (container.x + container.z);
                
            if (rect.y < container.y)
                outOfBoundsHeight += container.y - rect.y;
            if (rect.y + rect.w > container.y + container.w)
                outOfBoundsHeight += (rect.y + rect.w) - (container.y + container.w);
                
            return outOfBoundsWidth * rect.w + outOfBoundsHeight * rect.z - (outOfBoundsWidth * outOfBoundsHeight);
        }
        
        [BurstCompile]
        public static float4 CalculateIntersection(float4 rect1, float4 rect2)
        {
            float xMin = math.max(rect1.x, rect2.x);
            float yMin = math.max(rect1.y, rect2.y);
            float xMax = math.min(rect1.x + rect1.z, rect2.x + rect2.z);
            float yMax = math.min(rect1.y + rect1.w, rect2.y + rect2.w);
            
            return new float4(xMin, yMin, math.max(0, xMax - xMin), math.max(0, yMax - yMin));
        }
        
        [BurstCompile]
        public static float CalculateIntersectionArea(float4 rect1, float4 rect2)
        {
            float4 intersection = CalculateIntersection(rect1, rect2);
            
            if (intersection.z <= 0 || intersection.w <= 0)
                return 0;
                
            return intersection.z * intersection.w;
        }
        
        [BurstCompile]
        public static float CalculateOutOfBoundsPercent(float4 container, float4 rect)
        {
            float menuArea = rect.z * rect.w;
            if (menuArea <= 0) return 0;
            
            float outOfBoundsArea = CalculateOutOfBoundsArea(container, rect);
            return outOfBoundsArea / menuArea;
        }
        
        [BurstCompile]
        public static void CalculateOutOfBoundsPercentages(
            ref float outOfBoundsPercentTop, 
            ref float outOfBoundsPercentBottom, 
            ref float outOfBoundsPercentRight, 
            ref float outOfBoundsPercentLeft,
            float4 menuRect, float4 boundaryRect, float menuWidth, float menuHeight)
        {
            if (menuRect.y + menuRect.w > boundaryRect.y + boundaryRect.w)
            {
                float overflow = (menuRect.y + menuRect.w) - (boundaryRect.y + boundaryRect.w);
                outOfBoundsPercentTop = overflow / menuHeight;
            }
            
            if (menuRect.y < boundaryRect.y)
            {
                float overflow = boundaryRect.y - menuRect.y;
                outOfBoundsPercentBottom = overflow / menuHeight;
            }
            
            if (menuRect.x + menuRect.z > boundaryRect.x + boundaryRect.z)
            {
                float overflow = (menuRect.x + menuRect.z) - (boundaryRect.x + boundaryRect.z);
                outOfBoundsPercentRight = overflow / menuWidth;
            }
            
            if (menuRect.x < boundaryRect.x)
            {
                float overflow = boundaryRect.x - menuRect.x;
                outOfBoundsPercentLeft = overflow / menuWidth;
            }
        }
        
        /// <summary>
        /// Convert Unity Rect to float4
        /// </summary>
        public static float4 RectToFloat4(Rect rect)
        {
            return new float4(rect.x, rect.y, rect.width, rect.height);
        }
        
        /// <summary>
        /// Convert float4 to Unity Rect (for interop with Unity API)
        /// </summary>
        public static Rect Float4ToRect(float4 rect)
        {
            return new Rect(rect.x, rect.y, rect.z, rect.w);
        }
    }
} 