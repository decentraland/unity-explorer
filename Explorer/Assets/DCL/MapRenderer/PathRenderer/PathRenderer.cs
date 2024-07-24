using UnityEngine;

namespace DCL.MapRenderer
{
    [RequireComponent(typeof(LineRenderer))]
    public class PathRenderer : MonoBehaviour
    {
        public Transform startPoint;
        public Vector3 endPoint;
        public float dotSize = 0.2f;
        public float spaceBetweenDots = 0.2f;
        public float updateMagnitude = 1;
        public float arrivalTolerance = 100;

        private LineRenderer lineRenderer;
        private Vector2 cachedPosition;

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            SetupLineRenderer();
        }

        private void SetupLineRenderer()
        {
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = dotSize * 2;
            lineRenderer.endWidth = dotSize * 2;
            lineRenderer.numCapVertices = 5;

            Material lineMaterial = lineRenderer.material;
            float textureRepeat = 1f / (dotSize + spaceBetweenDots);
            lineMaterial.mainTextureScale = new Vector2(textureRepeat, 1);
            lineMaterial.mainTextureOffset = new Vector2(0f, 0f);
        }

        public void SetOrigin(Transform transform)
        {
            startPoint = transform;
        }

        public void SetDestination(Vector3 destination)
        {
            endPoint = destination;
            UpdateLine(true);
        }

        public void UpdateLine(bool force = false)
        {
            if (endPoint == Vector3.zero || startPoint == null) return;

            if (!force && !(Mathf.Abs(cachedPosition.sqrMagnitude - startPoint.position.sqrMagnitude) > updateMagnitude)) return;

            cachedPosition = startPoint.position;
            SetupLineRenderer();
            Vector3 direction = endPoint - startPoint.position;
            float distance = direction.magnitude;

            float totalUnitLength = dotSize + spaceBetweenDots;
            int numberOfDots = Mathf.FloorToInt(distance / totalUnitLength) + 1;

            lineRenderer.positionCount = numberOfDots * 2;

            for (int i = 0; i < numberOfDots; i++)
            {
                float startT = i * totalUnitLength / distance;
                float endT = startT + dotSize / distance;

                Vector3 dotStart = Vector3.Lerp(startPoint.position, endPoint, startT);
                Vector3 dotEnd = Vector3.Lerp(startPoint.position, endPoint, endT);

                lineRenderer.SetPosition(i * 2, dotStart);
                lineRenderer.SetPosition(i * 2 + 1, dotEnd);
            }
        }

        // Optional: Update in real-time
        private void Update()
        {
            UpdateLine();
        }
    }
}
