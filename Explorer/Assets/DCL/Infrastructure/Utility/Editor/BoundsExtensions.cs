using UnityEngine;

namespace Utility.Editor
{
    public static class BoundsExtensions
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="color"></param>
        public static void DrawInEditor(this Bounds bounds, Color color)
        {
            Vector3 halfSize = bounds.size * 0.5f;
            Vector3[] bottomCorners = new Vector3[4]
                    {
                        new Vector3(bounds.center.x - halfSize.x, bounds.center.y - halfSize.y, bounds.center.z + halfSize.z),
                        new Vector3(bounds.center.x + halfSize.x, bounds.center.y - halfSize.y, bounds.center.z + halfSize.z),
                        new Vector3(bounds.center.x + halfSize.x, bounds.center.y - halfSize.y, bounds.center.z - halfSize.z),
                        new Vector3(bounds.center.x - halfSize.x, bounds.center.y - halfSize.y, bounds.center.z - halfSize.z),
                    };
            Vector3[] topCorners = new Vector3[4]
                    {
                        new Vector3(bottomCorners[0].x, bottomCorners[0].y + bounds.size.y, bottomCorners[0].z),
                        new Vector3(bottomCorners[1].x, bottomCorners[1].y + bounds.size.y, bottomCorners[1].z),
                        new Vector3(bottomCorners[2].x, bottomCorners[2].y + bounds.size.y, bottomCorners[2].z),
                        new Vector3(bottomCorners[3].x, bottomCorners[3].y + bounds.size.y, bottomCorners[3].z),
                    };

            Debug.DrawLine(bottomCorners[0], topCorners[0], color);
            Debug.DrawLine(bottomCorners[1], topCorners[1], color);
            Debug.DrawLine(bottomCorners[2], topCorners[2], color);
            Debug.DrawLine(bottomCorners[3], topCorners[3], color);

            Debug.DrawLine(bottomCorners[0], bottomCorners[1], color);
            Debug.DrawLine(bottomCorners[1], bottomCorners[2], color);
            Debug.DrawLine(bottomCorners[2], bottomCorners[3], color);
            Debug.DrawLine(bottomCorners[3], bottomCorners[0], color);

            Debug.DrawLine(topCorners[0], topCorners[1], color);
            Debug.DrawLine(topCorners[1], topCorners[2], color);
            Debug.DrawLine(topCorners[2], topCorners[3], color);
            Debug.DrawLine(topCorners[3], topCorners[0], color);
        }
    }
}
