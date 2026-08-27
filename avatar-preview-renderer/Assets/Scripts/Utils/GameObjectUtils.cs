using UnityEngine;

namespace Utils
{
    public static class GameObjectUtils
    {
        public static void CenterAndFit(Transform root, Camera mainCamera, float wearablePadding = 0.15f)
        {
            if (!TryGetCombinedBounds(root, out var combined)) return;

            // Make it a cube
            var maxSize = Mathf.Max(combined.size.x, Mathf.Max(combined.size.y, combined.size.z));
            combined = new Bounds(combined.center, Vector3.one * maxSize);

            // Get local center of bounds and move them parent position (0, 0, 0 unless something changes)
            var localCenter = root.InverseTransformPoint(combined.center);
            combined.center = root.parent.position;

            // Desired object size in world units with padding
            var size = combined.size; // * (1f + wearablePadding);

            float scaleFactor;
            if (mainCamera.orthographic)
            {
                // World-window dimensions for orthographic camera
                var orthoHeight = mainCamera.orthographicSize * 2f;
                var orthoWidth = orthoHeight * mainCamera.aspect;
                var orthoMin = Mathf.Min(orthoWidth, orthoHeight);
                scaleFactor = orthoMin / size.x;
            }
            else
            {
                // Distance from camera to object after centering
                var distance = Vector3.Distance(mainCamera.transform.position, combined.center);

                // Camera frustum size at that distance
                var frustumHeight = 2f * distance * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                var frustumWidth = frustumHeight * mainCamera.aspect;
                var frustumMin = Mathf.Min(frustumWidth, frustumHeight);
                scaleFactor = frustumMin * (1f - wearablePadding * 2f) / size.x;
            }

            root.localScale *= scaleFactor;

            // Centre by moving the CONTENT onto the root, never the root off its content. The root is
            // what DragRotator spins, and Transform.Rotate pivots on the root's own origin, so any gap
            // between the two becomes the radius of an orbit. Only the horizontal part of the gap
            // matters for a yaw spin, which is why items modelled on the body's vertical axis were
            // fine and one modelled out to the side - a watch at the wrist - swung out of frame, its
            // offset multiplied by the large scale a small item gets fitted with.
            root.localPosition = Vector3.zero;

            foreach (Transform child in root)
                child.localPosition -= localCenter;
        }

        /// <summary>
        /// Measures a subject so the result does not change as it spins about Y. The radius is the
        /// circumradius of the horizontal footprint, which is the widest half-width the bounds can
        /// present at any yaw, so framing against it never clips part way through a rotation. Returns
        /// false when the subject has nothing to measure.
        /// </summary>
        public static bool TryMeasureYawInvariant(Transform root, out Vector3 center, out float radius,
            out float height)
        {
            center = default(Vector3);
            radius = 0f;
            height = 0f;

            if (!TryGetCombinedBounds(root, out var bounds)) return false;

            var size = bounds.size;

            center = bounds.center;
            radius = 0.5f * Mathf.Sqrt(size.x * size.x + size.z * size.z);
            height = size.y;

            return true;
        }

        private static bool TryGetCombinedBounds(Transform root, out Bounds bounds)
        {
            bounds = default(Bounds);

            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return false;

            bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return true;
        }
    }
}
