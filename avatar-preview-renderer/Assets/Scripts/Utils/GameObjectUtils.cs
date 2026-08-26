using UnityEngine;

namespace Utils
{
    public static class GameObjectUtils
    {
        public static void CenterAndFit(Transform root, Camera mainCamera, float wearablePadding = 0.15f)
        {
            // Gather combined bounds of all Renderers under root
            var renders = root.GetComponentsInChildren<Renderer>();
            if (renders.Length == 0) return;
            var combined = renders[0].bounds;
            for (var i = 1; i < renders.Length; i++)
                combined.Encapsulate(renders[i].bounds);

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

            // Apply uniform scaling and adjust position on root
            root.localScale *= scaleFactor;
            root.localPosition = Vector3.Scale(-localCenter, root.localScale);
        }
    }
}