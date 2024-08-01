using UnityEngine;

namespace DCL.MapRenderer
{
    [RequireComponent(typeof(LineRenderer))]
    public class MapPathRenderer : MonoBehaviour
    {
        private const float DOT_SIZE = 20;
        private const int NUM_CAP_VERTICES = 5;
        private const float SPACE_BETWEEN_DOTS = 10;

        public Vector2 DestinationPoint => destinationPoint;

        private bool destinationSet;
        private Vector2 destinationPoint;
        private LineRenderer lineRenderer;
        private Vector2 originPoint;

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            SetupLineRenderer();
        }

        private void SetupLineRenderer()
        {
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = DOT_SIZE;
            lineRenderer.endWidth = DOT_SIZE;
            lineRenderer.numCapVertices = NUM_CAP_VERTICES;

            Material lineMaterial = lineRenderer.material;
            float textureRepeat = 1f / (DOT_SIZE + SPACE_BETWEEN_DOTS);
            lineMaterial.mainTextureScale = new Vector2(textureRepeat, 1);
            lineMaterial.mainTextureOffset = new Vector2(0f, 0f);
        }

        public void SetDestination(Vector2 destination)
        {
            destinationSet = true;
            destinationPoint = destination;
            UpdateLine();
        }

        public void UpdateOrigin(Vector2 origin, bool updateLine = false)
        {
            originPoint = origin;

            if (updateLine && destinationSet) { UpdateLine(); }
        }

        private void UpdateLine()
        {
            Vector3 direction = destinationPoint - originPoint;
            float distance = direction.magnitude;

            float totalUnitLength = DOT_SIZE + SPACE_BETWEEN_DOTS;
            int numberOfDots = Mathf.FloorToInt(distance / totalUnitLength) + 1;

            lineRenderer.positionCount = numberOfDots * 2;

            for (var i = 0; i < numberOfDots; i++)
            {
                float startT = i * totalUnitLength / distance;
                float endT = startT + (DOT_SIZE / distance);

                var dotStart = Vector3.Lerp(originPoint, destinationPoint, startT);
                var dotEnd = Vector3.Lerp(originPoint, destinationPoint, endT);

                lineRenderer.SetPosition(i * 2, dotStart);
                lineRenderer.SetPosition((i * 2) + 1, dotEnd);
            }
        }
    }
}
